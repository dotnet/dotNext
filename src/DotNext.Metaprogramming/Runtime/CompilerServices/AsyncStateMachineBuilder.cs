using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using static System.Linq.Enumerable;

namespace DotNext.Runtime.CompilerServices
{
    using Linq.Expressions;
    using Reflection;
    using static Collections.Generic.Dictionary;
    using static Collections.Generic.Sequence;
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
        private static readonly UserDataSlot<int> ParameterPositionSlot = UserDataSlot<int>.Allocate();

        // small optimization - reuse variable for awaiters of the same type
        private sealed class VariableEqualityComparer : IEqualityComparer<ParameterExpression>
        {
            public bool Equals(ParameterExpression? x, ParameterExpression? y)
                => AwaitExpression.IsAwaiterHolder(x) && AwaitExpression.IsAwaiterHolder(y) ? x.Type == y.Type : object.Equals(x, y);

            public int GetHashCode(ParameterExpression variable)
                => AwaitExpression.IsAwaiterHolder(variable) ? variable.Type.GetHashCode() : variable.GetHashCode();
        }

        internal readonly TaskType Task;
        internal readonly IDictionary<ParameterExpression, MemberExpression?> Variables;
        private readonly VisitorContext context;

        // this label indicates end of async method when successful result should be returned
        internal readonly LabelTarget AsyncMethodEnd;

        // a table with labels in the beginning of async state machine
        private readonly StateTransitionTable stateSwitchTable;

        internal AsyncStateMachineBuilder(Type taskType, IReadOnlyList<ParameterExpression> parameters)
        {
            Task = new TaskType(taskType);
            Variables = new Dictionary<ParameterExpression, MemberExpression?>(new VariableEqualityComparer());
            for (var position = 0; position < parameters.Count; position++)
            {
                var parameter = parameters[position];
                MarkAsParameter(parameter, position);
                Variables.Add(parameter, null);
            }

            context = new VisitorContext(out AsyncMethodEnd);
            stateSwitchTable = new StateTransitionTable();
        }

        private static void MarkAsParameter(ParameterExpression parameter, int position)
            => parameter.GetUserData().Set(ParameterPositionSlot, position);

        internal IEnumerable<ParameterExpression> Parameters
            => from candidate in Variables.Keys
               let position = candidate.GetUserData().Get(ParameterPositionSlot, -1)
               where position >= 0
               orderby position ascending
               select candidate;

        private ParameterExpression NewStateSlot(Type type)
            => NewStateSlot(() => Expression.Variable(type));

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
                Statement.Rewrite(ref node);
                node.Variables.ForEach(variable => Variables.Add(variable, null));
                node = node.Update(Empty<ParameterExpression>(), node.Expressions);
                return context.Rewrite(node, base.VisitBlock);
            }
            else
            {
                return VisitBlock(Expression.Block(typeof(void), node.Variables, node.Expressions));
            }
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if (node.Type == typeof(void))
            {
                node = node.Update(node.Test, Statement.Wrap(node.IfTrue), Statement.Wrap(node.IfFalse));
                return context.Rewrite(node, base.VisitConditional);
            }
            else if (node.IfTrue is BlockExpression && node.IfFalse is BlockExpression)
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

                if ((ExpressionAttributes.Get(node.IfTrue)?.ContainsAwait ?? false) || (ExpressionAttributes.Get(node.IfFalse)?.ContainsAwait ?? false))
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
            => context.Rewrite(node, base.VisitLambda);

        protected override Expression VisitListInit(ListInitExpression node)
            => context.Rewrite(node, base.VisitListInit);

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
            => context.Rewrite(node, base.VisitTypeBinary);

        protected override Expression VisitSwitch(SwitchExpression node)
        {
            if (node.Type == typeof(void))
            {
                Statement.Rewrite(ref node);
                return context.Rewrite(node, base.VisitSwitch);
            }
            else
            {
                throw new NotSupportedException(ExceptionMessages.VoidSwitchExpected);
            }
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

        private Expression VisitAsyncResult(AsyncResultExpression expr)
        {
            if (context.IsInFinally)
                throw new InvalidOperationException(ExceptionMessages.LeavingFinallyClause);

            // attach all available finalization code
            var prologue = context.CurrentStatement.PrologueCodeInserter();
            expr = (AsyncResultExpression)base.VisitExtension(expr);

            foreach (var finalization in context.CreateJumpPrologue(AsyncMethodEnd.Goto(), this))
                prologue(finalization);
            return expr;
        }

        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case StatePlaceholderExpression placeholder:
                    return placeholder;
                case AsyncResultExpression result:
                    return VisitAsyncResult(result);
                case AwaitExpression await:
                    return context.Rewrite(await, VisitAwait);
                case RecoverFromExceptionExpression recovery:
                    Variables.Add(recovery.Receiver, null);
                    return recovery;
                case StateMachineExpression sme:
                    return sme;
                default:
                    return context.Rewrite(node, base.VisitExtension);
            }
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
            var leftIsAsync = ExpressionAttributes.Get(node.Left)?.ContainsAwait ?? false;
            var rightIsAsync = ExpressionAttributes.Get(node.Right)?.ContainsAwait ?? false;

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
                hasAwait |= ExpressionAttributes.Get(arg)?.ContainsAwait ?? false;
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
            => context.Rewrite(node, n => RewriteCallable(n, n.Arguments.ToArray(), base.VisitIndex, UpdateArguments));

        private static NewExpression UpdateArguments(NewExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(arguments);

        protected override Expression VisitNew(NewExpression node)
            => context.Rewrite(node, n => RewriteCallable(n, n.Arguments.ToArray(), base.VisitNew, UpdateArguments));

        private static NewArrayExpression UpdateArguments(NewArrayExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(arguments);

        protected override Expression VisitNewArray(NewArrayExpression node)
            => context.Rewrite(node, n => RewriteCallable(n, n.Expressions.ToArray(), base.VisitNewArray, UpdateArguments));

        protected override Expression VisitLoop(LoopExpression node)
        {
            if (node.Type == typeof(void))
            {
                Statement.Rewrite(ref node);
                return context.Rewrite(node, base.VisitLoop);
            }
            else
            {
                throw new NotSupportedException(ExceptionMessages.VoidLoopExpected);
            }
        }

        // do not rewrite the body of inner lambda expression
        [return: NotNullIfNotNull("node")]
        public override Expression? Visit(Expression? node)
            => node is LambdaExpression ? node.ReduceExtensions() : base.Visit(node);

        protected override Expression VisitDynamic(DynamicExpression node)
            => context.Rewrite(node, base.VisitDynamic);

        protected override Expression VisitMember(MemberExpression node)
            => context.Rewrite(node, base.VisitMember);

        protected override Expression VisitMemberInit(MemberInitExpression node)
            => context.Rewrite(node, base.VisitMemberInit);

        private Expression Rethrow(UnaryExpression node)
        {
            var holder = context.ExceptionHolder;
            return holder is null ? new RethrowExpression() : RethrowExpression.Dispatch(holder);
        }

        protected override Expression VisitUnary(UnaryExpression node) => node.NodeType switch
        {
            ExpressionType.Throw when node.Operand is null => context.Rewrite(node, Rethrow),
            _ => context.Rewrite(node, base.VisitUnary)
        };

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
            Assert(stateMachine is not null);
            var stateVariable = Expression.Variable(GetStateField(stateMachine).Type);
            var parameters = methodBuilder.Parameters;
            ICollection<Expression> newBody = new LinkedList<Expression>();

            // save all parameters into fields
            foreach (var parameter in parameters)
                newBody.Add(methodBuilder.Variables[parameter]!.Update(stateVariable).Assign(parameter));
            var startMethod = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.Start));
            Debug.Assert(startMethod is not null);
            newBody.Add(methodBuilder.Task.AdjustTaskType(Expression.Call(startMethod, stateMachineMethod, stateVariable)));
            return Expression.Lambda<TDelegate>(Expression.Block(new[] { stateVariable }, newBody), true, parameters);
        }

        private sealed class StateMachineBuilder
        {
            private readonly Type returnType;
            internal ParameterExpression? StateMachine;

            internal StateMachineBuilder(Type returnType) => this.returnType = returnType;

            [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AsyncStateMachine<>))]
            [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AsyncStateMachine<,>))]
            internal MemberExpression Build(Type stateType)
            {
                var stateMachineType = returnType == typeof(void) ?
                    typeof(AsyncStateMachine<>).MakeGenericType(stateType) :
                    typeof(AsyncStateMachine<,>).MakeGenericType(stateType, returnType);
                stateMachineType = stateMachineType.MakeByRefType();
                return GetStateField(StateMachine = Expression.Parameter(stateMachineType));
            }
        }

        private static MemberExpression[] CreateStateHolderType(Type returnType, ParameterExpression[] variables, out ParameterExpression stateMachine)
        {
            var sm = new StateMachineBuilder(returnType);
            MemberExpression[] slots;
            using (var builder = new ValueTupleBuilder())
            {
                foreach (var v in variables)
                    builder.Add(v.Type);
                slots = builder.Build(sm.Build, out _);
            }

            Assert(sm.StateMachine is not null);
            stateMachine = sm.StateMachine;
            return slots;
        }

        private static ParameterExpression CreateStateHolderType(Type returnType, IDictionary<ParameterExpression, MemberExpression?> variables)
        {
            var vars = variables.Keys.ToArray();
            var slots = CreateStateHolderType(returnType, vars, out var stateMachine);
            for (var i = 0L; i < slots.LongLength; i++)
                variables[vars[i]] = slots[i];
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
            Assert(stateMachine is not null);
            return node switch
            {
                StatePlaceholderExpression placeholder => placeholder.Reduce(),
                AsyncResultExpression result => Visit(result.Reduce(stateMachine, methodBuilder.AsyncMethodEnd)),
                StateMachineExpression sme => Visit(sme.Reduce(stateMachine)),
                Statement statement => Visit(statement.Reduce()),
                _ => base.VisitExtension(node),
            };
        }

        internal Expression<TDelegate> Build(Expression body, bool tailCall)
        {
            body = methodBuilder.Rewrite(body) ?? Expression.Empty();

            // build state machine type
            stateMachine = CreateStateHolderType(methodBuilder.Task.ResultType, methodBuilder.Variables);

            // replace all special expressions
            body = Visit(body);

            // now we have state machine method, wrap it into lambda
            return Build(BuildStateMachine(body, stateMachine, tailCall));
        }

        public void Dispose() => methodBuilder.Dispose();
    }
}
