using System;
using System.Collections.Generic;
using static System.Linq.Enumerable;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using Metaprogramming;
    using Reflection;
    using static Collections.Generic.Collections;
    using static Collections.Generic.Dictionaries;

    /// <summary>
    /// Provides initial transformation of async method.
    /// </summary>
    /// <remarks>
    /// Transformation steps:
    /// 1. Identify all local variables
    /// 2. Construct state holder type
    /// 3. Replace all local variables with fields from state holder type
    /// </remarks>
    internal sealed class AsyncStateMachineBuilder : ExpressionVisitor, IDisposable
    {
        
        private static readonly UserDataSlot<int> ParameterPositionSlot = UserDataSlot<int>.Allocate();

        //small optimization - reuse variable for awaiters of the same type
        private sealed class VariableEqualityComparer : IEqualityComparer<ParameterExpression>
        {
            public bool Equals(ParameterExpression x, ParameterExpression y)
                => AwaitExpression.IsAwaiterHolder(x) && AwaitExpression.IsAwaiterHolder(y) ? x.Type == y.Type : object.Equals(x, y);

            public int GetHashCode(ParameterExpression variable)
                => AwaitExpression.IsAwaiterHolder(variable) ? variable.Type.GetHashCode() : variable.GetHashCode();
        }

        internal readonly Type AsyncReturnType;
        internal readonly IDictionary<ParameterExpression, MemberExpression> Variables;
        private readonly VisitorContext context;
        //this label indicates end of async method when successful result should be returned
        internal readonly LabelTarget AsyncMethodEnd;
        //a table with labels in the beginning of async state machine
        private readonly StateTransitionTable stateSwitchTable;

        internal AsyncStateMachineBuilder(Type returnType, IReadOnlyList<ParameterExpression> parameters)
        {
            if (returnType is null)
                throw new ArgumentException("Invalid return type of async method");
            AsyncReturnType = returnType;
            Variables = new Dictionary<ParameterExpression, MemberExpression>(new VariableEqualityComparer());
            for (var position = 0; position < parameters.Count; position++)
            {
                var parameter = parameters[position];
                MarkAsParameter(parameter, position);
                Variables.Add(parameter, null);
            }
            context = new VisitorContext();
            AsyncMethodEnd = Expression.Label("end_async_method");
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

        //async method cannot have block expression with type not equal to void
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
                return VisitBlock(Expression.Block(typeof(void), node.Variables, node.Expressions));
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if (node.Type == typeof(void))
            {
                node = node.Update(node.Test, Statement.Wrap(node.IfTrue), Statement.Wrap(node.IfFalse));
                return context.Rewrite(node, base.VisitConditional);
            }
            else if (node.IfTrue is BlockExpression && node.IfFalse is BlockExpression)
                throw new NotSupportedException("A branch of conditional expression is invalid");
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
                var prologue = context.CurrentStatement.CapturePrologueWriter();
                {
                    var result = context.Rewrite(node, base.VisitConditional);
                    if (result is ConditionalExpression)
                        node = (ConditionalExpression)result;
                    else
                        return result;
                }
                if (ExpressionAttributes.Get(node.IfTrue).ContainsAwait || ExpressionAttributes.Get(node.IfFalse).ContainsAwait)
                {
                    var tempVar = NewStateSlot(node.Type);
                    prologue(Expression.Condition(node.Test, Expression.Assign(tempVar, node.IfTrue), Expression.Assign(tempVar, node.IfFalse), typeof(void)));
                    return tempVar;
                }
                else
                    return node;
            }
        }

        protected override Expression VisitLabel(LabelExpression node)
            => node.Type == typeof(void) ? new Statement(node) : throw new NotSupportedException("Label should be of type Void");

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
                throw new NotSupportedException("Switch-case expression must of type Void");
        }

        protected override Expression VisitGoto(GotoExpression node)
            => node.Type == typeof(void) ? new Statement(node) : throw new NotSupportedException("Goto expression should of type Void");

        protected override Expression VisitDebugInfo(DebugInfoExpression node)
            => context.Rewrite(node, base.VisitDebugInfo);

        protected override Expression VisitDefault(DefaultExpression node)
            => context.Rewrite(node, base.VisitDefault);

        protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
            => context.Rewrite(node, base.VisitRuntimeVariables);

        protected override SwitchCase VisitSwitchCase(SwitchCase node)
        {
            var testValues = Visit(node.TestValues, tst => context.Rewrite(tst, Visit));
            var body = context.Rewrite(node.Body, Visit);
            return node.Update(testValues, body);
        }

        protected override ElementInit VisitElementInit(ElementInit node)
        {
            var arguments = Visit(node.Arguments, arg => context.Rewrite(arg, Visit));
            return node.Update(arguments);
        }

        protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
        {
            var expression = context.Rewrite(node.Expression, Visit);
            return ReferenceEquals(expression, node.Expression) ? node : node.Update(expression);
        }

        protected override CatchBlock VisitCatchBlock(CatchBlock node)
            => throw new NotSupportedException();

        //try-catch will be completely replaced with flat code and set of switch-case-goto statements
        protected override Expression VisitTry(TryExpression node)
            => context.Rewrite(node, stateSwitchTable, base.VisitExtension);

        private Expression VisitAwait(AwaitExpression node)
        {
            var prologue = context.CurrentStatement.CapturePrologueWriter();
            node = (AwaitExpression)base.VisitExtension(node);
            //allocate slot for awaiter
            var awaiterSlot = NewStateSlot(node.NewAwaiterHolder);
            //generate new state and label for it
            var (stateId, transition) = context.NewTransition(stateSwitchTable);
            //convert await expression into TAwaiter.GetResult() expression
            return node.Reduce(awaiterSlot, stateId, transition.Successful, AsyncMethodEnd, prologue);
        }

        private Expression VisitAsyncResult(AsyncResultExpression expr)
        {
            if (context.IsInFinally)
                throw new InvalidOperationException("Control cannot leave the body of a finally clause");
            //attach all available finalization code
            var prologue = context.CurrentStatement.CapturePrologueWriter();
            foreach (var finalization in context.FinalizationCode(this))
                prologue(finalization);
            return expr;
        }

        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
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

        private static bool IsAssignment(BinaryExpression binary)
        {
            switch(binary.NodeType)
            {
                case ExpressionType.Assign:
                case ExpressionType.AddAssign:
                case ExpressionType.AddAssignChecked:
                case ExpressionType.SubtractAssign:
                case ExpressionType.SubtractAssignChecked:
                case ExpressionType.OrAssign:
                case ExpressionType.AndAssign:
                case ExpressionType.ExclusiveOrAssign:
                case ExpressionType.DivideAssign:
                case ExpressionType.LeftShiftAssign:
                case ExpressionType.RightShiftAssign:
                case ExpressionType.MultiplyAssign:
                case ExpressionType.MultiplyAssignChecked:
                case ExpressionType.ModuloAssign:
                case ExpressionType.PostDecrementAssign:
                case ExpressionType.PreDecrementAssign:
                case ExpressionType.PostIncrementAssign:
                case ExpressionType.PreIncrementAssign:
                case ExpressionType.PowerAssign:
                    return true;
                default:
                    return false;
            }
        }

        private Expression RewriteBinary(BinaryExpression node)
        {
            var codeInsertionPoint = context.CurrentStatement.CapturePrologueWriter();
            var newNode = base.VisitBinary(node);
            if (newNode is BinaryExpression)
                node = (BinaryExpression)newNode;
            else
                return newNode;
            //do not place left operand at statement level because it has no side effects
            if (node.Left is ParameterExpression || node.Left is ConstantExpression || IsAssignment(node))
                return node;
            var leftIsAsync = ExpressionAttributes.Get(node.Left).ContainsAwait;
            var rightIsAsync = ExpressionAttributes.Get(node.Right).ContainsAwait;
            //left operand should be computed before right, so bump it before await expression
            if (rightIsAsync && !leftIsAsync)
            {
                /*
                    Method() + await a;
                    --transformed into--
                    state.field = Method();
                    state.awaitor = a.GetAwaitor();
                    MoveNext(state.awaitor, newState);
                    return;
                    newState: state.field + state.awaitor.GetResult();
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

        private Expression RewriteCallable<E>(E node, Expression[] arguments, Converter<E, Expression> visitor, Func<E, Expression[], E> updater)
            where E: Expression
        {
            var codeInsertionPoint = context.CurrentStatement.CapturePrologueWriter();
            var newNode = visitor(node);
            if(newNode is E)
                node = (E)newNode;
            else
                return newNode;
            var hasAwait = false;
            for(var i = arguments.LongLength - 1L; i >= 0L; i--)
            {
                ref Expression arg = ref arguments[i];
                if(ExpressionAttributes.Get(arg).ContainsAwait)
                    hasAwait = true;
                else if(hasAwait)
                {
                    var tempVar = NewStateSlot(arg.Type);
                    codeInsertionPoint(Expression.Assign(tempVar, arg));
                    arg = tempVar;
                }
            }
            return updater(node, arguments);
        }

        private static MethodCallExpression UpdateArguments(MethodCallExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(node.Object, arguments);

        protected override Expression VisitMethodCall(MethodCallExpression node)
            => context.Rewrite(node, n => RewriteCallable(n, n.Arguments.ToArray(), base.VisitMethodCall, UpdateArguments));

        private static InvocationExpression UpdateArguments(InvocationExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(node.Expression, arguments);

        protected override Expression VisitInvocation(InvocationExpression node)
            => context.Rewrite(node, n => RewriteCallable(n, n.Arguments.ToArray(), base.VisitInvocation, UpdateArguments));
        
        private static IndexExpression UpdateArguments(IndexExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(node.Object, arguments);

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
                throw new NotSupportedException("Loop expression should be of type Void");
        }

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

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch(node.NodeType)
            {
                case ExpressionType.Throw:
                    if (node.Operand is null)
                        return context.Rewrite(node, Rethrow);
                    else
                        goto default;
                default:
                    return context.Rewrite(node, base.VisitUnary);
            }
        }

        private SwitchExpression MakeSwitch()
        {
            ICollection<SwitchCase> cases = new LinkedList<SwitchCase>();
            foreach (var (state, label) in stateSwitchTable)
                cases.Add(Expression.SwitchCase(label.MakeGoto(), state.AsConst()));
            return Expression.Switch(new StateIdExpression(), Expression.Empty(), null, cases);
        }

        internal BlockExpression Rewrite(Expression body)
        {
            body = body is BlockExpression block ?
                Expression.Block(typeof(void), block.Variables, block.Expressions) :
                Expression.Block(typeof(void), body);
            body = Visit(body);
            return Expression.Block(body.Type, Array.Empty<ParameterExpression>(),
                Sequence.Single(MakeSwitch()).Concat(((BlockExpression)body).Expressions).Concat(Sequence.Single(AsyncMethodEnd.LandingSite())));
        }

        public void Dispose()
        {
            Variables.Clear();
            stateSwitchTable.Clear();
            context.Dispose();
        }
    }

    internal sealed class AsyncStateMachineBuilder<D>: ExpressionVisitor, IDisposable
        where D: Delegate
    {   
        private readonly AsyncStateMachineBuilder methodBuilder;
        private ParameterExpression stateMachine;

        internal AsyncStateMachineBuilder(IReadOnlyList<ParameterExpression> parameters)
        {
            var invokeMethod = Delegates.GetInvokeMethod<D>();
            methodBuilder = new AsyncStateMachineBuilder(invokeMethod?.ReturnType?.GetTaskType(), parameters);
        }

        private static LambdaExpression BuildStateMachine(Expression body, ParameterExpression stateMachine, bool tailCall)
        {
            var delegateType = stateMachine.Type.GetNestedType(nameof(AsyncStateMachine<ValueTuple>.Transition));
            delegateType = delegateType.MakeGenericType(stateMachine.Type.GetGenericArguments());
            return Expression.Lambda(delegateType, body, tailCall, stateMachine);
        }

        private static MemberExpression GetStateField(ParameterExpression stateMachine)
            => stateMachine.Field(nameof(AsyncStateMachine<int>.State));

        private Expression<D> Build(LambdaExpression stateMachineMethod)
        {
            var stateVariable = Expression.Variable(GetStateField(stateMachine).Type);
            var parameters = methodBuilder.Parameters;
            ICollection<Expression> newBody = new LinkedList<Expression>();
            //save all parameters into fields
            foreach (var parameter in parameters)
                newBody.Add(methodBuilder.Variables[parameter].Update(stateVariable).Assign(parameter));
            newBody.Add(Expression.Call(null, stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.Start)), stateMachineMethod, stateVariable));
            return Expression.Lambda<D>(Expression.Block(new[] { stateVariable }, newBody), true, parameters);
        }

        private static MemberExpression[] CreateStateHolderType(Type returnType, ParameterExpression[] variables, out ParameterExpression stateMachine)
        {
            ParameterExpression sm = null;
            MemberExpression MakeStateHolder(Type stateType)
            {
                var stateMachineType = returnType == typeof(void) ?
                    typeof(AsyncStateMachine<>).MakeGenericType(stateType) :
                    typeof(AsyncStateMachine<,>).MakeGenericType(stateType, returnType);
                stateMachineType = stateMachineType.MakeByRefType();
                sm = Expression.Parameter(stateMachineType);
                return GetStateField(sm);
            }
            MemberExpression[] slots;
            using (var builder = new ValueTupleBuilder())
            {
                variables.ForEach(variable => builder.Add(variable.Type));
                slots = builder.Build(MakeStateHolder, out _);
            }
            stateMachine = sm;
            return slots;
        }

        private static ParameterExpression CreateStateHolderType(Type returnType, IDictionary<ParameterExpression, MemberExpression> variables)
        {
            var vars = variables.Keys.ToArray();
            var slots = CreateStateHolderType(returnType, vars, out var stateMachine);
            for (var i = 0L; i < slots.LongLength; i++)
                variables[vars[i]] = slots[i];
            return stateMachine;
        }

        //replace local vairables with appropriate state fields
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (methodBuilder.Variables.TryGetValue(node, out var stateSlot))
                return stateSlot;
            else
                return node;
        }

        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case AsyncResultExpression result:
                    return Visit(result.Reduce(stateMachine, methodBuilder.AsyncMethodEnd));
                case StateMachineExpression sme:
                    return Visit(sme.Reduce(stateMachine));
                case Statement statement:
                    return Visit(statement.Reduce());
                default:
                    return base.VisitExtension(node);
            }
        }

        internal Expression<D> Build(Expression body, bool tailCall)
        {
            body = methodBuilder.Rewrite(body);
            //build state machine type
            stateMachine = CreateStateHolderType(methodBuilder.AsyncReturnType, methodBuilder.Variables);
            //replace all special expressions
            body = Visit(body);
            //now we have state machine method, wrap it into lambda
            return Build(BuildStateMachine(body, stateMachine, tailCall));
        }

        public void Dispose() => methodBuilder.Dispose();
    }
}
