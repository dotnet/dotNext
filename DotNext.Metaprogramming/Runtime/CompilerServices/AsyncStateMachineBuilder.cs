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
            AsyncMethodEnd = VisitorContext.CompilerGeneratedLabelTarget("end_async_method");
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
                node.Variables.ForEach(variable => Variables.Add(variable, null));
                switch (node.Expressions.Count)
                {
                    case 0:
                        return Expression.Empty();
                    case 1:
                        return context.Rewrite(node.Expressions[0], Visit);
                    default:
                        node = node.Update(Array.Empty<ParameterExpression>(), node.Expressions);
                        return context.Rewrite(node, base.VisitBlock);
                }
            }
            else
                return VisitBlock(Expression.Block(typeof(void), node.Variables, node.Expressions));
        }

        private Expression RewriteConditional(ConditionalExpression node)
        {

            var result = base.VisitConditional(node);
            if (result is ConditionalExpression)
                node = (ConditionalExpression)result;
            else
                return result;
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
            if (VisitorContext.ContainsAwait(node.IfTrue) || VisitorContext.ContainsAwait(node.IfFalse))
            {
                var tempVar = NewStateSlot(node.Type);
                context.GetStatementPrologueWriter().Invoke(Expression.Condition(node.Test, Expression.Assign(tempVar, node.IfTrue), Expression.Assign(tempVar, node.IfFalse), typeof(void)));
                return tempVar;
            }
            else
                return node;
        }

        protected override Expression VisitConditional(ConditionalExpression node)
            => node.Type != typeof(void) && (node.IfTrue is BlockExpression || node.IfFalse is BlockExpression) ?
                throw new NotSupportedException("A branch of conditional expression is invalid") :
                context.Rewrite(node, RewriteConditional);

        protected override LabelTarget VisitLabelTarget(LabelTarget node)
            => node.Type == typeof(void) ? base.VisitLabelTarget(node) : throw new NotSupportedException("Label should be of type Void");

        protected override Expression VisitLambda<T>(Expression<T> node)
            => context.Rewrite(node, base.VisitLambda);

        protected override Expression VisitListInit(ListInitExpression node)
            => context.Rewrite(node, base.VisitListInit);

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
            => context.Rewrite(node, base.VisitTypeBinary);
        
        protected override Expression VisitSwitch(SwitchExpression node)
            => node.Type == typeof(void) ?
                context.Rewrite(node, base.VisitSwitch) :
                throw new NotSupportedException("Switch-case expression must of type Void");

        protected override Expression VisitGoto(GotoExpression node)
            => node.Type == typeof(void) ?
                context.Rewrite(node, base.VisitGoto) :
                throw new NotSupportedException("Goto expression should of type Void");

        protected override CatchBlock VisitCatchBlock(CatchBlock node)
            => throw new NotSupportedException();

        private static void Prepare(ref TryExpression node)
        {
            ICollection<CatchBlock> handlers = new LinkedList<CatchBlock>();
            foreach (var handler in node.Handlers)
                if (handler.Variable is null)
                    handlers.Add(handler.Update(Expression.Variable(handler.Test, "e"), handler.Filter, handler.Body));
                else
                    handlers.Add(handler);
            node = node.Update(node.Body, handlers, node.Finally, node.Fault);
        }

        private Expression VisitTryOnly(TryExpression expression) => Visit(expression.Body);

        //try-catch will be completely replaced with flat code and set of switch-case-goto statements
        protected override Expression VisitTry(TryExpression node)
        {
            if (node.Type == typeof(void))
            {
                /*
                 * Code in FINALLY clause will be inserted into each escape point
                 * inside of try clause
                 */
                Prepare(ref node);
                var tryBody = this.context.Rewrite(node, VisitTryOnly);
                var context = VisitorContext.RemoveTryCatchContext(node);
                tryBody = context.MakeTryBody(tryBody);
                //try-catch OR try-catch-finally
                if (node.Handlers.Count > 0)
                {
                    var handlers = new LinkedList<ConditionalExpression>();
                    foreach (var catchBlock in node.Handlers)
                    {
                        Variables.Add(catchBlock.Variable, null);
                        handlers.AddLast(context.RecoveryContext.MakeCatchBlock(catchBlock, stateSwitchTable, this));
                    }
                    tryBody = tryBody.AddEpilogue(false, handlers);
                }
                //insert recovery state if needed
                //try-finally or try-fault
                var @finally = context.MakeFaultBody(node.Finally ?? node.Fault, stateSwitchTable, this);
                return tryBody.AddEpilogue(false, @finally);
            }
            else
                throw new NotSupportedException("Try-Catch statement should be of type Void");
        }

        private Expression VisitAwait(AwaitExpression node)
        {
            node = (AwaitExpression)base.VisitExtension(node);
            context.ContainsAwait();
            //allocate slot for awaiter
            var awaiterSlot = NewStateSlot(node.NewAwaiterHolder);
            //generate new state and label for it
            var (stateId, transition) = context.NewTransition(stateSwitchTable);
            //convert await expression into TAwaiter.GetResult() expression
            return node.Reduce(awaiterSlot, stateId, transition.Successful, AsyncMethodEnd, context.GetStatementPrologueWriter());
        }

        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case AwaitExpression await:
                    return context.Rewrite(await, VisitAwait);
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
            var codeInsertionPoint = context.GetStatementPrologueWriter();
            var newNode = base.VisitBinary(node);
            if (newNode is BinaryExpression)
                node = (BinaryExpression)newNode;
            else
                return newNode;
            //do not place left operand at statement level because it has no side effects
            if (node.Left is ParameterExpression || node.Left is ConstantExpression || IsAssignment(node))
                return node;
            var leftIsAsync = VisitorContext.ContainsAwait(node.Left);
            var rightIsAsync = VisitorContext.ContainsAwait(node.Right);
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

        private Expression RewriteCallable<E>(E node, Expression[] arguments, Converter<E, Expression> visitor, Func<E, Expression[], E> updater)
            where E: Expression
        {
            var codeInsertionPoint = context.GetStatementPrologueWriter();
            var newNode = visitor(node);
            if(newNode is E)
                node = (E)newNode;
            else
                return newNode;
            var hasAwait = false;
            for(var i = arguments.LongLength - 1L; i >= 0L; i--)
            {
                ref Expression arg = ref arguments[i];
                if(VisitorContext.ContainsAwait(arg))
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
            => node.Type == typeof(void) ? context.Rewrite(node, base.VisitLoop) : throw new NotSupportedException("Loop expression should be of type Void");

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
            context.Clear();
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

        public void Dispose()
        {
            methodBuilder.Dispose();
        }
    }
}
