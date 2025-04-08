﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static System.Linq.Enumerable;

namespace DotNext.Runtime.CompilerServices;

using Linq.Expressions;
using Reflection;
using static Collections.Generic.Dictionary;
using static Collections.Generic.Collection;
using static Reflection.TypeExtensions;

/// <summary>
/// Provides initial transformation of async method.
/// </summary>
/// <remarks>
/// Transformation steps:
/// 1. Identify all local variables
/// 2. Construct state holder type
/// 3. Replace all local variables with fields from state holder type.
/// </remarks>
internal sealed class AsyncStateMachineBuilder : ExpressionVisitor, IDisposable
{
    private static readonly UserDataSlot<int> ParameterPositionSlot = new();

    // small optimization - reuse variable for awaiters of the same type
    private sealed class VariableEqualityComparer : IEqualityComparer<ParameterExpression>
    {
        public bool Equals(ParameterExpression? x, ParameterExpression? y)
            => AwaitExpression.IsAwaiterHolder(x) && AwaitExpression.IsAwaiterHolder(y) ? x.Type == y.Type : object.Equals(x, y);

        public int GetHashCode(ParameterExpression variable)
            => AwaitExpression.IsAwaiterHolder(variable) ? variable.Type.GetHashCode() : variable.GetHashCode();
    }

    internal readonly TaskType Task;
    internal readonly Dictionary<ParameterExpression, MemberExpression?> Variables;
    private readonly VisitorContext context;

    // this label indicates end of async method when successful result should be returned
    internal readonly LabelTarget AsyncMethodEnd;

    // a table with labels in the beginning of async state machine
    private readonly StateTransitionTable stateSwitchTable;

    internal AsyncStateMachineBuilder(Type taskType, IReadOnlyList<ParameterExpression> parameters)
    {
        Task = new TaskType(taskType);
        Variables = new(new VariableEqualityComparer());
        for (var position = 0; position < parameters.Count; position++)
        {
            var parameter = parameters[position];
            MarkAsParameter(parameter, position);
            Variables.Add(parameter, null);
        }

        context = new VisitorContext(out AsyncMethodEnd);
        stateSwitchTable = new StateTransitionTable();
    }

    internal ParameterExpression? ResultVariable
    {
        get;
        private set;
    }

    private static void MarkAsParameter(ParameterExpression parameter, int position)
        => parameter.GetUserData().Set(ParameterPositionSlot, position);

    internal IEnumerable<ParameterExpression> Parameters
        => from candidate in Variables.Keys
           let position = candidate.GetUserData().Get(ParameterPositionSlot, -1)
           where position >= 0
           orderby position ascending
           select candidate;

    internal IEnumerable<ParameterExpression> Closures => Variables.Keys.Where(ClosureAnalyzer.IsClosure);

    private ParameterExpression NewStateSlot(Type type)
        => NewStateSlot(new Func<Type, ParameterExpression>(Expression.Variable).Bind(type));

    private ParameterExpression NewStateSlot(Func<ParameterExpression> factory)
    {
        var slot = factory();
        Variables[slot] = null;
        return slot;
    }

    // async method cannot have block expression with type not equal to void
    protected override Expression VisitBlock(BlockExpression node)
    {
        if (node.Type == typeof(void))
        {
            node = Statement.Rewrite(node);
            node.Variables.ForEach(variable => Variables.Add(variable, null));
            node = node.Update(Empty<ParameterExpression>(), node.Expressions);
            return context.Rewrite(node, base.VisitBlock);
        }

        return VisitBlock(Expression.Block(typeof(void), node.Variables, node.Expressions));
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        if (node.Type == typeof(void))
        {
            node = node.Update(node.Test, Statement.Wrap(node.IfTrue), Statement.Wrap(node.IfFalse));
            return context.Rewrite(node, base.VisitConditional);
        }
        else if (node is { IfTrue: BlockExpression, IfFalse: BlockExpression })
        {
            throw new NotSupportedException(ExceptionMessages.UnsupportedConditionalExpr);
        }
        else
        {
            /*
                x = a ? await b() : c();
                --transformed into--
                var temp;
                if(a)
                    temp = await b();
                else
                    temp = c();
                x = temp;
             */
            var prologue = context.CurrentStatement.PrologueCodeInserter();
            {
                var result = context.Rewrite(node, base.VisitConditional);
                if (result is ConditionalExpression conditional)
                    node = conditional;
                else
                    return result;
            }

            if (ExpressionAttributes.Get(node.IfTrue) is { ContainsAwait: true } || ExpressionAttributes.Get(node.IfFalse) is { ContainsAwait: true })
            {
                var tempVar = NewStateSlot(node.Type);
                prologue(Expression.Condition(node.Test, Expression.Assign(tempVar, node.IfTrue), Expression.Assign(tempVar, node.IfFalse), typeof(void)));
                return tempVar;
            }
            else
            {
                return node;
            }
        }
    }

    protected override Expression VisitLabel(LabelExpression node)
        => node.Type == typeof(void) ? context.Rewrite(node, Converter.Identity<LabelExpression>()) : throw new NotSupportedException(ExceptionMessages.VoidLabelExpected);

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        // inner lambda may have closures, we must handle this accordingly
        var analyzer = new ClosureAnalyzer(Variables);
        var lambda = (LambdaExpression)analyzer.Visit(node);

        return analyzer.Closures.Count > 0 ? new ClosureExpression(lambda, analyzer.Closures) : lambda;
    }

    protected override Expression VisitListInit(ListInitExpression node)
        => context.Rewrite(node, base.VisitListInit);

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        => context.Rewrite(node, base.VisitTypeBinary);

    protected override Expression VisitSwitch(SwitchExpression node)
    {
        if (node.Type != typeof(void))
            throw new NotSupportedException(ExceptionMessages.VoidSwitchExpected);

        node = Statement.Rewrite(node);
        return context.Rewrite(node, base.VisitSwitch);
    }

    protected override Expression VisitGoto(GotoExpression node)
    {
        node = context.Rewrite(node, Converter.Identity<GotoExpression>());
        return node.AddPrologue(false, context.CreateJumpPrologue(node, this));
    }

    protected override Expression VisitDebugInfo(DebugInfoExpression node)
        => context.Rewrite(node, base.VisitDebugInfo);

    protected override Expression VisitDefault(DefaultExpression node)
        => context.Rewrite(node, base.VisitDefault);

    protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
        => context.Rewrite(node, base.VisitRuntimeVariables);

    protected override SwitchCase VisitSwitchCase(SwitchCase node)
    {
        Converter<Expression, Expression> visitor = Visit;
        var testValues = Visit(node.TestValues, tst => context.Rewrite(tst, visitor));
        var body = context.Rewrite(node.Body, visitor);
        return node.Update(testValues, body);
    }

    protected override ElementInit VisitElementInit(ElementInit node)
    {
        var arguments = Visit(node.Arguments, arg => context.Rewrite(arg, new Converter<Expression, Expression>(Visit)));
        return node.Update(arguments);
    }

    protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
    {
        var expression = context.Rewrite(node.Expression, new Converter<Expression, Expression>(Visit));
        return ReferenceEquals(expression, node.Expression) ? node : node.Update(expression);
    }

    protected override CatchBlock VisitCatchBlock(CatchBlock node)
        => throw new NotSupportedException();

    // try-catch will be completely replaced with flat code and set of switch-case-goto statements
    protected override Expression VisitTry(TryExpression node)
        => context.Rewrite(node, stateSwitchTable, base.VisitExtension);

    private Expression VisitAwait(AwaitExpression node)
    {
        var prologue = context.CurrentStatement.PrologueCodeInserter();
        node = (AwaitExpression)base.VisitExtension(node);

        // allocate slot for awaiter
        var awaiterSlot = NewStateSlot(node.NewAwaiterHolder);

        // generate new state and label for it
        var (stateId, transition) = context.NewTransition(stateSwitchTable);

        // convert await expression into TAwaiter.GetResult() expression
        return node.Reduce(awaiterSlot, stateId, transition.Successful ?? throw new InvalidOperationException(), AsyncMethodEnd, prologue);
    }

    private AsyncResultExpression VisitAsyncResult(AsyncResultExpression expr)
    {
        if (context.IsInFinally)
            throw new InvalidOperationException(ExceptionMessages.LeavingFinallyClause);

        // attach all available finalization code
        var prologue = context.CurrentStatement.PrologueCodeInserter();
        expr = (AsyncResultExpression)base.VisitExtension(expr);

        if (Task.HasResult && expr.IsSimpleResult is false)
        {
            ResultVariable ??= Expression.Parameter(Task.ResultType);
            prologue(Expression.Assign(ResultVariable, expr.AsyncResult));
            expr = expr.Update(ResultVariable);
        }

        foreach (var finalization in context.CreateJumpPrologue(AsyncMethodEnd.Goto(), this))
            prologue(finalization);

        return expr;
    }

    protected override Expression VisitExtension(Expression node)
    {
        switch (node)
        {
            case StatePlaceholderExpression:
                break;
            case AsyncResultExpression result:
                node = VisitAsyncResult(result);
                break;
            case AwaitExpression await:
                node = context.Rewrite(await, VisitAwait);
                break;
            case RecoverFromExceptionExpression recovery:
                Variables.Add(recovery.Receiver, null);
                break;
            case StateMachineExpression:
                break;
            default:
                node = context.Rewrite(node, base.VisitExtension);
                break;
        }

        return node;
    }

    private static bool IsAssignment(BinaryExpression binary) => binary.NodeType is ExpressionType.Assign or
        ExpressionType.AddAssign or
        ExpressionType.AddAssignChecked or
        ExpressionType.SubtractAssign or
        ExpressionType.SubtractAssignChecked or
        ExpressionType.OrAssign or
        ExpressionType.AndAssign or
        ExpressionType.ExclusiveOrAssign or
        ExpressionType.DivideAssign or
        ExpressionType.LeftShiftAssign or
        ExpressionType.RightShiftAssign or
        ExpressionType.MultiplyAssign or
        ExpressionType.MultiplyAssignChecked or
        ExpressionType.ModuloAssign or
        ExpressionType.PostDecrementAssign or
        ExpressionType.PreDecrementAssign or
        ExpressionType.PostIncrementAssign or
        ExpressionType.PreIncrementAssign or
        ExpressionType.PowerAssign;

    private Expression RewriteBinary(BinaryExpression node)
    {
        var codeInsertionPoint = context.CurrentStatement.PrologueCodeInserter();
        var newNode = base.VisitBinary(node);
        if (newNode is BinaryExpression binary)
            node = binary;
        else
            return newNode;

        // do not place left operand at statement level because it has no side effects
        if (node.Left is ParameterExpression || node.Left is ConstantExpression || IsAssignment(node))
            return node;
        var leftIsAsync = ExpressionAttributes.Get(node.Left) is { ContainsAwait: true };
        var rightIsAsync = ExpressionAttributes.Get(node.Right) is { ContainsAwait: true };

        // left operand should be computed before right, so bump it before await expression
        if (rightIsAsync && !leftIsAsync)
        {
            /*
                Method() + await a;
                --transformed into--
                state.field = Method();
                state.awaiter = a.GetAwaiter();
                MoveNext(state.awaiter, newState);
                return;
                newState: state.field + state.awaiter.GetResult();
             */
            var leftTemp = NewStateSlot(node.Left.Type);
            codeInsertionPoint(Expression.Assign(leftTemp, node.Left));
            node = node.Update(leftTemp, node.Conversion, node.Right);
        }

        return node;
    }

    protected override Expression VisitBinary(BinaryExpression node)
        => context.Rewrite(node, RewriteBinary);

    protected override Expression VisitParameter(ParameterExpression node)
        => context.Rewrite(node, base.VisitParameter);

    protected override Expression VisitConstant(ConstantExpression node)
        => context.Rewrite(node, base.VisitConstant);

    private Expression RewriteCallable<TException>(TException node, Expression[] arguments, Converter<TException, Expression> visitor, Func<TException, Expression[], TException> updater)
        where TException : Expression
    {
        var newNode = visitor(node);
        if (newNode is TException typedExpr)
            node = typedExpr;
        else
            return newNode;

        var hasAwait = false;
        var codeInsertionPoint = context.CurrentStatement.PrologueCodeInserter();
        for (var i = arguments.LongLength - 1L; i >= 0L; i--)
        {
            ref Expression arg = ref arguments[i];
            hasAwait |= ExpressionAttributes.Get(arg) is { ContainsAwait: true };
            if (hasAwait)
            {
                var tempVar = NewStateSlot(arg.Type);
                codeInsertionPoint(Expression.Assign(tempVar, arg));
                arg = tempVar;
            }
        }

        return updater(node, arguments);
    }

    private static MethodCallExpression UpdateArguments(MethodCallExpression node, IReadOnlyCollection<Expression> arguments)
        => node.Update(node.Object!, arguments);

    protected override Expression VisitMethodCall(MethodCallExpression node)
        => context.Rewrite(node, n => RewriteCallable(n, n.Arguments.ToArray(), base.VisitMethodCall, UpdateArguments));

    private static InvocationExpression UpdateArguments(InvocationExpression node, IReadOnlyCollection<Expression> arguments)
        => node.Update(node.Expression, arguments);

    protected override Expression VisitInvocation(InvocationExpression node)
        => context.Rewrite(node, n => RewriteCallable(n, n.Arguments.ToArray(), base.VisitInvocation, UpdateArguments));

    private static IndexExpression UpdateArguments(IndexExpression node, IReadOnlyCollection<Expression> arguments)
        => node.Update(node.Object!, arguments);

    protected override Expression VisitIndex(IndexExpression node)
        => context.Rewrite(node, n => RewriteCallable(n, [.. n.Arguments], base.VisitIndex, UpdateArguments));

    private static NewExpression UpdateArguments(NewExpression node, IReadOnlyCollection<Expression> arguments)
        => node.Update(arguments);

    protected override Expression VisitNew(NewExpression node)
        => context.Rewrite(node, n => RewriteCallable(n, [.. n.Arguments], base.VisitNew, UpdateArguments));

    private static NewArrayExpression UpdateArguments(NewArrayExpression node, IReadOnlyCollection<Expression> arguments)
        => node.Update(arguments);

    protected override Expression VisitNewArray(NewArrayExpression node)
        => context.Rewrite(node, n => RewriteCallable(n, [.. n.Expressions], base.VisitNewArray, UpdateArguments));

    protected override Expression VisitLoop(LoopExpression node)
    {
        if (node.Type != typeof(void))
            throw new NotSupportedException(ExceptionMessages.VoidLoopExpected);

        node = Statement.Rewrite(node);
        return context.Rewrite(node, base.VisitLoop);
    }

    protected override Expression VisitDynamic(DynamicExpression node)
        => context.Rewrite(node, base.VisitDynamic);

    protected override Expression VisitMember(MemberExpression node)
        => context.Rewrite(node, base.VisitMember);

    protected override Expression VisitMemberInit(MemberInitExpression node)
        => context.Rewrite(node, base.VisitMemberInit);

    private Expression Rethrow(UnaryExpression node)
        => context.ExceptionHolder is { } holder ? RethrowExpression.Dispatch(holder) : new RethrowExpression();

    protected override Expression VisitUnary(UnaryExpression node)
        => context.Rewrite<UnaryExpression, Expression>(node, node is { NodeType: ExpressionType.Throw, Operand: null } ? Rethrow : base.VisitUnary);

    private SwitchExpression MakeSwitch()
    {
        ICollection<SwitchCase> cases = new LinkedList<SwitchCase>();
        foreach (var (state, label) in stateSwitchTable)
            cases.Add(Expression.SwitchCase(label.MakeGoto(), state.Const()));
        return Expression.Switch(new StateIdExpression(), Expression.Empty(), null, cases);
    }

    private Expression? Rewrite(Statement body)
        => Visit(body)?.Reduce().AddPrologue(false, MakeSwitch()).AddEpilogue(false, AsyncMethodEnd.LandingSite());

    internal Expression? Rewrite(Expression body)
        => Rewrite(body is BlockExpression block ?
            new Statement(Expression.Block(typeof(void), block.Variables, block.Expressions)) :
            new Statement(body));

    public void Dispose()
    {
        Variables.Clear();
        stateSwitchTable.Clear();
        context.Dispose();
    }
}

internal sealed class AsyncStateMachineBuilder<TDelegate> : ExpressionVisitor, IDisposable
    where TDelegate : Delegate
{
    private readonly AsyncStateMachineBuilder methodBuilder;
    private ParameterExpression? stateMachine;

    internal AsyncStateMachineBuilder(IReadOnlyList<ParameterExpression> parameters)
    {
        var invokeMethod = DelegateType.GetInvokeMethod<TDelegate>();
        methodBuilder = new AsyncStateMachineBuilder(invokeMethod.ReturnType, parameters);
    }

    private static Type BuildTransitionDelegate(Type stateMachineType)
        => typeof(Transition<,>)
            .MakeGenericType(stateMachineType.GetGenericArguments(typeof(IAsyncStateMachine<>))[0], stateMachineType);

    private static LambdaExpression BuildStateMachine(Expression body, ParameterExpression stateMachine, bool tailCall)
        => Expression.Lambda(BuildTransitionDelegate(stateMachine.Type), body, tailCall, stateMachine);

    private static MemberExpression GetStateField(ParameterExpression stateMachine)
        => stateMachine.Field(nameof(AsyncStateMachine<int>.State));

    private Expression<TDelegate> Build(LambdaExpression stateMachineMethod)
    {
        Debug.Assert(stateMachine is not null);
        var stateVariable = Expression.Variable(GetStateField(stateMachine).Type);
        var parameters = methodBuilder.Parameters;
        ICollection<Expression> newBody = new LinkedList<Expression>();

        // initialize closure containers
        foreach (var localVar in methodBuilder.Closures)
        {
            if (methodBuilder.Variables[localVar]?.Expression is MemberExpression inner)
            {
                inner = inner.Update(stateVariable);
                newBody.Add(inner.Assign(inner.Type.New()));
            }
        }

        // save all parameters into fields
        foreach (var parameter in parameters)
        {
            var parameterHolder = methodBuilder.Variables[parameter];
            Debug.Assert(parameterHolder is not null);

            // detect closure
            if (ClosureAnalyzer.IsClosure(parameter) && parameterHolder.Expression is MemberExpression inner)
            {
                inner = inner.Update(stateVariable);
                parameterHolder = parameterHolder.Update(inner);
            }
            else
            {
                parameterHolder = parameterHolder.Update(stateVariable);
            }

            newBody.Add(parameterHolder.Assign(parameter));
        }

        var startMethod = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.Start));
        Debug.Assert(startMethod is not null);
        newBody.Add(methodBuilder.Task.AdjustTaskType(Expression.Call(startMethod, stateMachineMethod, stateVariable)));
        return Expression.Lambda<TDelegate>(Expression.Block([stateVariable], newBody), true, parameters);
    }

    private sealed class StateMachineBuilder
    {
        private readonly bool usePooling;
        private readonly Type returnType;
        internal ParameterExpression? StateMachine;

        internal StateMachineBuilder(Type returnType, bool usePooling)
        {
            this.returnType = returnType;
            this.usePooling = usePooling;
        }

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AsyncStateMachine<>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AsyncStateMachine<,>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PoolingAsyncStateMachine<>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PoolingAsyncStateMachine<,>))]
        internal MemberExpression Build(Type stateType)
        {
            Type stateMachineType;
            if (returnType == typeof(void))
            {
                stateMachineType = usePooling ? typeof(PoolingAsyncStateMachine<>) : typeof(AsyncStateMachine<>);
                stateMachineType = stateMachineType.MakeGenericType(stateType);
            }
            else
            {
                stateMachineType = usePooling ? typeof(PoolingAsyncStateMachine<,>) : typeof(AsyncStateMachine<,>);
                stateMachineType = stateMachineType.MakeGenericType(stateType, returnType);
            }

            stateMachineType = stateMachineType.MakeByRefType();
            return GetStateField(StateMachine = Expression.Parameter(stateMachineType));
        }
    }

    private static MemberExpression[] CreateStateHolderType(Type returnType, bool usePooling, ReadOnlySpan<ParameterExpression> variables, out ParameterExpression stateMachine)
    {
        var sm = new StateMachineBuilder(returnType, usePooling);
        MemberExpression[] slots;
        using (var builder = new ValueTupleBuilder())
        {
            foreach (var v in variables)
            {
                var type = ClosureAnalyzer.IsClosure(v)
                    ? typeof(StrongBox<>).MakeGenericType(v.Type)
                    : v.Type;
                builder.Add(type);
            }

            slots = builder.Build<MemberExpression>(sm.Build, out _);
        }

        Debug.Assert(sm.StateMachine is not null);
        stateMachine = sm.StateMachine;
        return slots;
    }

    private static ParameterExpression CreateStateHolderType(Type returnType, bool usePooling, IDictionary<ParameterExpression, MemberExpression?> variables)
    {
        var vars = variables.Keys.ToArray();
        var slots = CreateStateHolderType(returnType, usePooling, vars, out var stateMachine);
        for (var i = 0L; i < slots.LongLength; i++)
        {
            var v = vars[i];
            var s = slots[i];
            variables[v] = ClosureAnalyzer.IsClosure(v) ? s.Field(nameof(StrongBox<int>.Value)) : s;
        }

        return stateMachine;
    }

    // replace local variables with appropriate state fields
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (methodBuilder.Variables.TryGetValue(node, out var stateSlot))
        {
            Debug.Assert(stateSlot is not null);
            return stateSlot;
        }

        return node;
    }

    protected override Expression VisitExtension(Expression node)
    {
        Debug.Assert(stateMachine is not null);
        return node switch
        {
            StatePlaceholderExpression placeholder => placeholder.Reduce(),
            AsyncResultExpression result => Visit(result.Reduce(stateMachine, methodBuilder.AsyncMethodEnd)),
            StateMachineExpression sme => Visit(sme.Reduce(stateMachine)),
            Statement statement => Visit(statement.Reduce()),
            ClosureExpression closure => closure.Reduce(methodBuilder.Variables),
            _ => base.VisitExtension(node),
        };
    }

    internal Expression<TDelegate> Build(Expression body, bool tailCall, bool usePooling)
    {
        body = methodBuilder.Rewrite(body) ?? Expression.Empty();

        // build state machine type
        stateMachine = CreateStateHolderType(methodBuilder.Task.ResultType, usePooling, methodBuilder.Variables);

        // replace all special expressions
        body = Visit(body);

        if (methodBuilder.ResultVariable is { } resultVar)
        {
            body = body is BlockExpression block
                ? block.Update(block.Variables.Append(resultVar), block.Expressions)
                : Expression.Block(body.Type, [resultVar], body);
        }

        // now we have state machine method, wrap it into lambda
        return Build(BuildStateMachine(body, stateMachine, tailCall));
    }

    public void Dispose() => methodBuilder.Dispose();
}