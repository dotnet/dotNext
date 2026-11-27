using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

using Linq.Expressions;
using Patterns;
using Reflection;
using static Collections.Generic.Collection;

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
        Variables = new(VariableEqualityComparer.Instance);
        for (var position = 0; position < parameters.Count; position++)
        {
            var parameter = parameters[position];
            MarkAsParameter(parameter, position);
            Variables.Add(parameter, null);
        }

        context = new VisitorContext(out AsyncMethodEnd);
        stateSwitchTable = new StateTransitionTable();
    }

    // the result must survive suspension in finally block, so it's a state slot
    private ParameterExpression? resultSlot;

    private static void MarkAsParameter(ParameterExpression parameter, int position)
        => parameter.UserData.Set(ParameterPositionSlot, position);

    internal IEnumerable<ParameterExpression> Parameters
        => from candidate in Variables.Keys
           let position = candidate.UserData.Get(ParameterPositionSlot, -1)
           where position >= 0
           orderby position
           select candidate;

    internal IEnumerable<ParameterExpression> Closures => Variables.Keys.Where(ClosureAnalyzer.IsClosure);

    private ParameterExpression NewStateSlot(Type type)
        => NewStateSlot(Expression.Variable(type));

    private ParameterExpression NewStateSlot(AwaitExpression expr)
        => NewStateSlot(expr.NewAwaiterHolder());

    private ParameterExpression NewStateSlot(ParameterExpression slot)
    {
        Variables[slot] = null;
        return slot;
    }

    // async method cannot have block expression with type not equal to void
    protected override Expression VisitBlock(BlockExpression node)
    {
        if (node.Type.IsVoid)
        {
            node = Statement.Rewrite(node);
            node.Variables.ForEach(variable => Variables.Add(variable, null));
            node = node.Update([], node.Expressions);
            return context.Rewrite(node, base.VisitBlock);
        }

        return VisitBlock(Expression.Block(typeof(void), node.Variables, node.Expressions));
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        if (node.Type.IsVoid)
        {
            node = node.Update(node.Test, Statement.Wrap(node.IfTrue), Statement.Wrap(node.IfFalse));
            return context.Rewrite(node, base.VisitConditional);
        }

        if (node is { IfTrue: BlockExpression } or { IfFalse: BlockExpression })
        {
            throw new NotSupportedException(ExceptionMessages.UnsupportedConditionalExpr);
        }

        if (AwaitDetector.ContainsAwait(node.IfTrue) || AwaitDetector.ContainsAwait(node.IfFalse))
        {
            return context.Rewrite(node, LowerConditional);
        }

        return context.Rewrite(node, base.VisitConditional);
    }

    // The branch with await expression must be lowered to the statement. Otherwise, the suspension point
    // would be placed in the prologue of the enclosing statement and evaluated regardless of the condition.
    private ParameterExpression LowerConditional(ConditionalExpression node)
    {
        /*
            x = a ? await b() : c();
            --transformed into--
            if (a)
                state.temp = await b();
            else
                state.temp = c();
            x = state.temp;
         */
        var result = NewStateSlot(node.Type);
        var statement = Visit(Expression.IfThenElse(node.Test, Expression.Assign(result, node.IfTrue), Expression.Assign(result, node.IfFalse)));

        // after the code produced by the test expression
        context.CurrentStatement.PrologueCodeInserter().Invoke(statement);
        context.MarkContainsAwait();
        return result;
    }

    private ParameterExpression LowerShortCircuit(BinaryExpression node)
    {
        if (node.Method is not null || node.Conversion is not null || (node.NodeType is not ExpressionType.Coalesce && node.Type != typeof(bool)))
            throw new NotSupportedException(ExceptionMessages.UnsupportedAsyncShortCircuit);

        var left = Visit(node.Left);
        var insertionPoint = context.CurrentStatement.PrologueCodeInserter();
        var result = NewStateSlot(node.Type);
        Expression statement;
        if (node.NodeType is ExpressionType.Coalesce)
        {
            /*
                x = a ?? await b();
                --transformed into--
                state.left = a;
                if (state.left != null)
                    state.temp = state.left;
                else
                    state.temp = await b();
                x = state.temp;
             */
            var leftValue = NewStateSlot(left.Type);
            insertionPoint(Expression.Assign(leftValue, left));
            Expression hasValue = Nullable.GetUnderlyingType(left.Type) is null
                ? Expression.ReferenceNotEqual(leftValue, Expression.Constant(null, left.Type))
                : Expression.Property(leftValue, nameof(Nullable<>.HasValue));

            statement = Expression.IfThenElse(
                hasValue,
                Expression.Assign(result, ConvertIfNeeded(leftValue, node.Type)),
                Expression.Assign(result, ConvertIfNeeded(node.Right, node.Type)));
        }
        else
        {
            /*
                x = a && await b();
                --transformed into--
                state.temp = a;
                if (state.temp)
                    state.temp = await b();
                x = state.temp;
             */
            insertionPoint(Expression.Assign(result, left));
            statement = Expression.IfThen(
                node.NodeType is ExpressionType.AndAlso ? result : Expression.Not(result),
                Expression.Assign(result, node.Right));
        }

        insertionPoint(Visit(statement));
        context.MarkContainsAwait();
        return result;

        static Expression ConvertIfNeeded(Expression expression, Type type)
            => expression.Type == type ? expression : Expression.Convert(expression, type);
    }

    protected override LabelExpression VisitLabel(LabelExpression node)
        => node.Type.IsVoid
            ? context.Rewrite(node, Func<LabelExpression, LabelExpression>.Identity)
            : throw new NotSupportedException(ExceptionMessages.VoidLabelExpected);

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        // inner lambda may have closures, we must handle this accordingly
        var analyzer = new ClosureAnalyzer(Variables);
        var lambda = (LambdaExpression)analyzer.Visit(node);

        return analyzer.Closures.Count > 0 ? new ClosureExpression(lambda, analyzer.Closures) : lambda;
    }

    private static bool ContainsAwait(ElementInit initializer)
        => initializer.Arguments.Any(AwaitDetector.ContainsAwait);

    private static bool ContainsAwait(MemberBinding binding) => binding switch
    {
        MemberAssignment assignment => AwaitDetector.ContainsAwait(assignment.Expression),
        MemberMemberBinding nested => nested.Bindings.Any(ContainsAwait),
        MemberListBinding list => list.Initializers.Any(ContainsAwait),
        _ => false,
    };

    protected override Expression VisitListInit(ListInitExpression node)
        => node.Initializers.Any(ContainsAwait)
            ? context.Rewrite(node, LowerListInit)
            : context.Rewrite(node, base.VisitListInit);

    protected override Expression VisitMemberInit(MemberInitExpression node)
        => node.Bindings.Any(ContainsAwait)
            ? context.Rewrite(node, LowerMemberInit)
            : context.Rewrite(node, base.VisitMemberInit);

    // Each binding must be completed before the next one is evaluated, as in C#. Thus, the initializer
    // with the suspension point is lowered to the sequence of statements.
    private ParameterExpression LowerMemberInit(MemberInitExpression node)
    {
        /*
            x = new T { A = a(), B = await b() };
            --transformed into--
            state.temp = new T();
            state.temp.A = a();
            state.temp.B = await b();
            x = state.temp;
         */
        var result = NewStateSlot(node.Type);
        var statements = new List<Expression> { Expression.Assign(result, node.NewExpression) };
        LowerBindings(result, node.Bindings, statements);
        return LowerInitializer(result, statements);
    }

    private ParameterExpression LowerListInit(ListInitExpression node)
    {
        /*
            x = new List<T> { a(), await b() };
            --transformed into--
            state.temp = new List<T>();
            state.temp.Add(a());
            state.temp.Add(await b());
            x = state.temp;
         */
        var result = NewStateSlot(node.Type);
        var statements = new List<Expression> { Expression.Assign(result, node.NewExpression) };
        LowerElements(result, node.Initializers, statements);
        return LowerInitializer(result, statements);
    }

    private ParameterExpression LowerInitializer(ParameterExpression result, List<Expression> statements)
    {
        var block = Visit(Expression.Block(typeof(void), statements));

        // after the code produced by the enclosing expressions
        context.CurrentStatement.PrologueCodeInserter().Invoke(block);
        context.MarkContainsAwait();
        return result;
    }

    private static void LowerBindings(Expression target, IEnumerable<MemberBinding> bindings, List<Expression> output)
    {
        foreach (var binding in bindings)
        {
            var member = Expression.MakeMemberAccess(target, binding.Member);
            switch (binding)
            {
                case MemberAssignment assignment:
                    output.Add(Expression.Assign(member, assignment.Expression));
                    break;
                case MemberMemberBinding nested:
                    LowerBindings(member, nested.Bindings, output);
                    break;
                case MemberListBinding list:
                    LowerElements(member, list.Initializers, output);
                    break;
            }
        }
    }

    private static void LowerElements(Expression target, IEnumerable<ElementInit> initializers, List<Expression> output)
    {
        foreach (var initializer in initializers)
            output.Add(Expression.Call(target, initializer.AddMethod, initializer.Arguments));
    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        => context.Rewrite(node, base.VisitTypeBinary);

    protected override Expression VisitSwitch(SwitchExpression node)
    {
        if (!node.Type.IsVoid)
            throw new NotSupportedException(ExceptionMessages.VoidSwitchExpected);

        node = Statement.Rewrite(node);
        return context.Rewrite(node, base.VisitSwitch);
    }

    protected override Expression VisitGoto(GotoExpression node)
    {
        node = context.Rewrite(node, Func<GotoExpression, GotoExpression>.Identity);
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
        var arguments = Visit(node.Arguments, arg => context.Rewrite<Expression, Expression>(arg, Visit));
        return node.Update(arguments);
    }

    protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
    {
        var expression = context.Rewrite<Expression, Expression>(node.Expression, Visit);
        return ReferenceEquals(expression, node.Expression) ? node : node.Update(expression);
    }

    protected override CatchBlock VisitCatchBlock(CatchBlock node)
        => throw new NotSupportedException();

    // try-catch will be completely replaced with flat code and set of switch-case-goto statements
    protected override Expression VisitTry(TryExpression node)
        => context.Rewrite(node, stateSwitchTable, base.VisitExtension);

    private MethodCallExpression VisitAwait(AwaitExpression node)
    {
        node = (AwaitExpression)base.VisitExtension(node);

        // suspension must be placed after the code produced by the operand
        var prologue = context.CurrentStatement.PrologueCodeInserter();

        // allocate slot for awaiter
        var awaiterSlot = NewStateSlot(node);

        // generate new state and label for it
        var (stateId, transition) = context.NewTransition(stateSwitchTable);

        // convert await expression into TAwaiter.GetResult() expression
        return node.Reduce(awaiterSlot, stateId, transition.Successful ?? throw new InvalidOperationException(), AsyncMethodEnd, prologue);
    }

    private AsyncResultExpression VisitAsyncResult(AsyncResultExpression expr)
    {
        if (context.IsInFinally)
            throw new InvalidOperationException(ExceptionMessages.LeavingFinallyClause);

        // attach all available finalization code after the code produced by the result expression
        expr = (AsyncResultExpression)base.VisitExtension(expr);
        var prologue = context.CurrentStatement.PrologueCodeInserter();

        // the result must be computed before finalization code that can modify local variables
        if (Task.HasResult && expr.IsSimpleResult is false)
        {
            resultSlot ??= NewStateSlot(Task.ResultType);
            prologue(Expression.Assign(resultSlot, expr.AsyncResult));
            expr = expr.Update(resultSlot);
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

    // use it after a node is visited
    private static bool ContainsAwait(Expression node)
        => ExpressionAttributes.Get(node) is { ContainsAwait: true };

    // stores the value of the expression in the state slot, so it can be evaluated before the suspension point
    private Expression Spill(Expression node, CodeInsertionPoint insertionPoint)
    {
        // these expressions produce the same value regardless of the evaluation order
        if (node is ConstantExpression or DefaultExpression or LambdaExpression)
            return node;

        var slot = NewStateSlot(node.Type);
        insertionPoint(Expression.Assign(slot, node));
        return slot;
    }

    // stores the parts of the location in the state slots, so the location remains the same
    // after the suspension point, but the location itself is not copied
    private Expression SpillLocation(Expression location, CodeInsertionPoint insertionPoint)
    {
        switch (location)
        {
            case ParameterExpression or MemberExpression { Expression: null }:
                return location;
            case MemberExpression member:
                return member.Update(SpillReceiver(member.Expression, insertionPoint));
            case IndexExpression { Object: not null } index:
                var receiver = SpillReceiver(index.Object, insertionPoint);
                var arguments = new Expression[index.Arguments.Count];
                for (var i = 0; i < arguments.Length; i++)
                    arguments[i] = Spill(index.Arguments[i], insertionPoint);

                return index.Update(receiver, arguments);
            case BinaryExpression { NodeType: ExpressionType.ArrayIndex } arrayIndex:
                return arrayIndex.Update(Spill(arrayIndex.Left, insertionPoint), arrayIndex.Conversion, Spill(arrayIndex.Right, insertionPoint));
            default:
                // not a location, so it can be copied
                return Spill(location, insertionPoint);
        }
    }

    // the receiver of value type must not be copied, because the copy can be mutated instead of the original value
    private Expression SpillReceiver(Expression receiver, CodeInsertionPoint insertionPoint)
        => receiver.Type.IsValueType ? SpillLocation(receiver, insertionPoint) : Spill(receiver, insertionPoint);

    // Visits operands in their evaluation order. The operands evaluated before the last await expression
    // are spilled to the state slots, so they are evaluated before the suspension point, as in C#.
    private void VisitOperands(Span<Expression?> operands, ReadOnlySpan<bool> isLocation)
    {
        var insertionPoints = new CodeInsertionPoint[operands.Length];
        var lastAsyncOperand = -1;
        for (var i = 0; i < operands.Length; i++)
        {
            if (operands[i] is { } operand)
            {
                operands[i] = operand = Visit(operand);
                if (ContainsAwait(operand))
                    lastAsyncOperand = i;
            }

            // the position after the code produced by the operand
            insertionPoints[i] = context.CurrentStatement.PrologueCodeInserter();
        }

        // in reverse order, because the insertion points of adjacent operands may share the same position
        for (var i = lastAsyncOperand - 1; i >= 0; i--)
        {
            if (operands[i] is { } operand)
            {
                operands[i] = i < isLocation.Length && isLocation[i]
                    ? SpillLocation(operand, insertionPoints[i])
                    : Spill(operand, insertionPoints[i]);
            }
        }
    }

    private static bool IsByRefParameter(ParameterInfo parameter) => parameter.ParameterType.IsByRef;

    private static bool[] GetLocationOperands(Expression? receiver, MethodBase? method)
    {
        var parameters = method?.GetParameters() ?? [];
        var result = new bool[parameters.Length + 1];
        result[0] = receiver?.Type.IsValueType ?? false;
        for (var i = 0; i < parameters.Length; i++)
            result[i + 1] = IsByRefParameter(parameters[i]);

        return result;
    }

    private static ExpressionType? GetCompoundOperation(ExpressionType assignment) => assignment switch
    {
        ExpressionType.AddAssign => ExpressionType.Add,
        ExpressionType.AddAssignChecked => ExpressionType.AddChecked,
        ExpressionType.SubtractAssign => ExpressionType.Subtract,
        ExpressionType.SubtractAssignChecked => ExpressionType.SubtractChecked,
        ExpressionType.MultiplyAssign => ExpressionType.Multiply,
        ExpressionType.MultiplyAssignChecked => ExpressionType.MultiplyChecked,
        ExpressionType.DivideAssign => ExpressionType.Divide,
        ExpressionType.ModuloAssign => ExpressionType.Modulo,
        ExpressionType.PowerAssign => ExpressionType.Power,
        ExpressionType.AndAssign => ExpressionType.And,
        ExpressionType.OrAssign => ExpressionType.Or,
        ExpressionType.ExclusiveOrAssign => ExpressionType.ExclusiveOr,
        ExpressionType.LeftShiftAssign => ExpressionType.LeftShift,
        ExpressionType.RightShiftAssign => ExpressionType.RightShift,
        _ => null,
    };

    private BinaryExpression RewriteBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var insertionPoint = context.CurrentStatement.PrologueCodeInserter();
        var right = Visit(node.Right);
        var conversion = VisitAndConvert(node.Conversion, nameof(VisitBinary));

        if (ContainsAwait(right))
        {
            if (GetCompoundOperation(node.NodeType) is { } operation)
            {
                /*
                    x += await a;
                    --transformed into--
                    state.field = x;
                    state.awaiter = a.GetAwaiter();
                    MoveNext(state.awaiter, newState);
                    return;
                    newState: x = state.field + state.awaiter.GetResult();
                 */
                left = SpillLocation(left, insertionPoint);
                var current = Spill(left, insertionPoint);
                Expression result = Expression.MakeBinary(operation, current, right, liftToNull: false, node.Method);
                if (conversion is not null)
                    result = Expression.Invoke(conversion, result);
                else if (result.Type != left.Type)
                    result = Expression.Convert(result, left.Type);

                return Expression.Assign(left, result);
            }

            /*
                Method() + await a;
                --transformed into--
                state.field = Method();
                state.awaiter = a.GetAwaiter();
                MoveNext(state.awaiter, newState);
                return;
                newState: state.field + state.awaiter.GetResult();
             */
            left = node.NodeType is ExpressionType.Assign
                ? SpillLocation(left, insertionPoint)
                : Spill(left, insertionPoint);
        }

        return node.Update(left, conversion, right);
    }

    protected override Expression VisitBinary(BinaryExpression node)
        => node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse or ExpressionType.Coalesce && AwaitDetector.ContainsAwait(node.Right)
            ? context.Rewrite(node, LowerShortCircuit)
            : context.Rewrite(node, RewriteBinary);

    protected override Expression VisitParameter(ParameterExpression node)
        => context.Rewrite(node, base.VisitParameter);

    protected override Expression VisitConstant(ConstantExpression node)
        => context.Rewrite(node, base.VisitConstant);

    private MethodCallExpression RewriteMethodCall(MethodCallExpression node)
    {
        Expression?[] operands = [node.Object, .. node.Arguments];
        VisitOperands(operands, GetLocationOperands(node.Object, node.Method));
        return node.Update(operands[0], operands[1..]!);
    }

    protected override MethodCallExpression VisitMethodCall(MethodCallExpression node)
        => context.Rewrite(node, RewriteMethodCall);

    private InvocationExpression RewriteInvocation(InvocationExpression node)
    {
        Expression?[] operands = [node.Expression, .. node.Arguments];
        var invokeMethod = node.Expression.Type.GetMethod(nameof(Action.Invoke));
        VisitOperands(operands, GetLocationOperands(receiver: null, invokeMethod));
        return node.Update(operands[0]!, operands[1..]!);
    }

    protected override InvocationExpression VisitInvocation(InvocationExpression node)
        => context.Rewrite(node, RewriteInvocation);

    private IndexExpression RewriteIndex(IndexExpression node)
    {
        Expression?[] operands = [node.Object, .. node.Arguments];
        VisitOperands(operands, [node.Object?.Type.IsValueType ?? false]);
        return node.Update(operands[0]!, operands[1..]!);
    }

    protected override IndexExpression VisitIndex(IndexExpression node)
        => context.Rewrite(node, RewriteIndex);

    private NewExpression RewriteNew(NewExpression node)
    {
        Expression?[] operands = [.. node.Arguments];
        VisitOperands(operands, GetLocationOperands(receiver: null, node.Constructor).AsSpan(1));
        return node.Update(operands!);
    }

    protected override NewExpression VisitNew(NewExpression node)
        => context.Rewrite(node, RewriteNew);

    private NewArrayExpression RewriteNewArray(NewArrayExpression node)
    {
        Expression?[] operands = [.. node.Expressions];
        VisitOperands(operands, []);
        return node.Update(operands!);
    }

    protected override NewArrayExpression VisitNewArray(NewArrayExpression node)
        => context.Rewrite(node, RewriteNewArray);

    protected override Expression VisitLoop(LoopExpression node)
    {
        if (!node.Type.IsVoid)
            throw new NotSupportedException(ExceptionMessages.VoidLoopExpected);

        node = Statement.Rewrite(node);
        return context.Rewrite(node, base.VisitLoop);
    }

    protected override Expression VisitDynamic(DynamicExpression node)
        => context.Rewrite(node, base.VisitDynamic);

    protected override Expression VisitMember(MemberExpression node)
        => context.Rewrite(node, base.VisitMember);

    private Expression Rethrow(UnaryExpression node)
        => context.ExceptionHolder is { } holder ? RethrowExpression.Dispatch(holder) : new RethrowExpression();

    protected override Expression VisitUnary(UnaryExpression node)
        => context.Rewrite<UnaryExpression, Expression>(node, node is { NodeType: ExpressionType.Throw, Operand: null } ? Rethrow : base.VisitUnary);

    private SwitchExpression MakeSwitch()
    {
        ICollection<SwitchCase> cases = new LinkedList<SwitchCase>();
        foreach (var (state, label) in stateSwitchTable)
            cases.Add(Expression.SwitchCase(label.MakeGoto(), state.Quoted));
        return Expression.Switch(new StateIdExpression(), Expression.Empty(), null, cases);
    }

    private Expression Rewrite(Statement body)
        => Visit(body).Reduce().AddPrologue(false, MakeSwitch()).AddEpilogue(false, AsyncMethodEnd.LandingSite());

    internal Expression Rewrite(Expression body)
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
        var invokeMethod = DelegateType.get_InvokeMethod<TDelegate>();
        methodBuilder = new AsyncStateMachineBuilder(invokeMethod.ReturnType, parameters);
    }

    private static Type BuildTransitionDelegate(Type stateMachineType)
        => typeof(Transition<,>)
            .MakeGenericType(stateMachineType.GetGenericArguments(typeof(IAsyncStateMachine<>))[0], stateMachineType);

    private static LambdaExpression BuildStateMachine(Expression body, ParameterExpression stateMachine, bool tailCall)
        => Expression.Lambda(BuildTransitionDelegate(stateMachine.Type), body, tailCall, stateMachine);

    private static MemberExpression GetStateField(ParameterExpression stateMachine)
        => stateMachine.Field(nameof(AsyncStateMachine<>.State));

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

        var startMethod = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<>.Start));
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
            if (returnType.IsVoid)
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

    private static ReadOnlySpan<MemberExpression> CreateStateHolderType(Type returnType, bool usePooling, ReadOnlySpan<ParameterExpression> variables,
        out ParameterExpression stateMachine)
    {
        var sm = new StateMachineBuilder(returnType, usePooling);
        var builder = new ValueTupleBuilder();
        foreach (var v in variables)
        {
            var type = ClosureAnalyzer.IsClosure(v)
                ? typeof(StrongBox<>).MakeGenericType(v.Type)
                : v.Type;
            builder.Add(type);
        }

        ReadOnlySpan<MemberExpression> slots = builder.Build<MemberExpression>(sm.Build, out _);

        Debug.Assert(sm.StateMachine is not null);
        stateMachine = sm.StateMachine;
        return slots;
    }

    private static ParameterExpression CreateStateHolderType(Type returnType, bool usePooling, IDictionary<ParameterExpression, MemberExpression?> variables)
    {
        ReadOnlySpan<ParameterExpression> vars = [.. variables.Keys];
        var slots = CreateStateHolderType(returnType, usePooling, vars, out var stateMachine);
        for (var i = 0; i < slots.Length; i++)
        {
            var v = vars[i];
            var s = slots[i];
            variables[v] = ClosureAnalyzer.IsClosure(v) ? s.Field(nameof(StrongBox<>.Value)) : s;
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
        body = methodBuilder.Rewrite(body);

        // build state machine type
        stateMachine = CreateStateHolderType(methodBuilder.Task.ResultType, usePooling, methodBuilder.Variables);

        // replace all special expressions
        body = Visit(body);

        // now we have state machine method, wrap it into lambda
        return Build(BuildStateMachine(body, stateMachine, tailCall));
    }

    public void Dispose() => methodBuilder.Dispose();
}

// Detects await expressions in the expression tree before its rewriting.
// Use it before a node is visited.
file sealed class AwaitDetector : ExpressionVisitor
{
    private bool found;

    [return: NotNullIfNotNull(nameof(node))]
    public override Expression? Visit(Expression? node) => found ? node : base.Visit(node);

    // await expression inside of the nested lambda belongs to that lambda
    protected override Expression VisitLambda<T>(Expression<T> node) => node;

    protected override Expression VisitExtension(Expression node)
    {
        if (node is AwaitExpression)
        {
            found = true;
            return node;
        }

        return node.CanReduce ? base.VisitExtension(node) : node;
    }

    public static bool ContainsAwait(Expression? node)
    {
        var detector = new AwaitDetector();
        detector.Visit(node);
        return detector.found;
    }
}

// small optimization - reuse variable for awaiters of the same type
file sealed class VariableEqualityComparer : IEqualityComparer<ParameterExpression>, ISingleton<VariableEqualityComparer>
{
    public static VariableEqualityComparer Instance { get; } = new();

    private VariableEqualityComparer()
    {
    }
    
    public bool Equals(ParameterExpression? x, ParameterExpression? y)
        => AwaitExpression.IsAwaiterHolder(x) && AwaitExpression.IsAwaiterHolder(y) ? x.Type == y.Type : object.Equals(x, y);

    public int GetHashCode(ParameterExpression variable)
        => AwaitExpression.IsAwaiterHolder(variable) ? variable.Type.GetHashCode() : variable.GetHashCode();
}