using System;
using System.Collections.Generic;
using static System.Linq.Enumerable;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;

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
        private static readonly UserDataSlot<bool> IsAwaiterVarSlot = UserDataSlot<bool>.Allocate();
        private static readonly UserDataSlot<int> ParameterPositionSlot = UserDataSlot<int>.Allocate();

        //small optimization - reuse variable for awaiters of the same type
        private sealed class VariableEqualityComparer : IEqualityComparer<ParameterExpression>
        {
            private static bool IsAwaiter(ParameterExpression variable) => variable.GetUserData().Get(IsAwaiterVarSlot);

            public bool Equals(ParameterExpression x, ParameterExpression y)
                => IsAwaiter(x) && IsAwaiter(y) ? x.Type == y.Type : object.Equals(x, y);

            public int GetHashCode(ParameterExpression variable)
                => IsAwaiter(variable) ? variable.Type.GetHashCode() : variable.GetHashCode();
        }

        internal readonly Type AsyncReturnType;
        internal readonly IDictionary<ParameterExpression, MemberExpression> Variables;
        private int stateId;
        private readonly VisitorContext context;
        //this label indicates end of async method when successful result should be returned
        internal readonly LabelTarget AsyncMethodEnd;
        //a table with labels in the beginning of async state machine
        private readonly IDictionary<int, LabelTarget> stateSwitchTable;

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
            stateId = AsyncStateMachine<ValueTuple>.FINAL_STATE;
            context = new VisitorContext();
            AsyncMethodEnd = Expression.Label("end_async_method");
            stateSwitchTable = new Dictionary<int, LabelTarget>();
        }

        private static void MarkAsParameter(ParameterExpression parameter, int position)
            => parameter.GetUserData().Set(ParameterPositionSlot, position);

        private static void MarkAsAwaiterVar(ParameterExpression variable)
            => variable.GetUserData().Set(IsAwaiterVarSlot, true);

        internal IEnumerable<ParameterExpression> Parameters
            => from candidate in Variables.Keys
               let position = candidate.GetUserData().Get(ParameterPositionSlot, -1)
               where position >= 0
               orderby position ascending
               select candidate;

        private static SwitchExpression MakeSwitch(Expression stateId, IDictionary<int, LabelTarget> switchTable)
        {
            ICollection<SwitchCase> cases = new LinkedList<SwitchCase>();
            foreach (var (state, label) in switchTable)
                cases.Add(Expression.SwitchCase(label.Goto(), state.AsConst()));
            return Expression.Switch(stateId, Expression.Empty(), null, cases);
        }

        private int NextState() => ++stateId;
        
        private ParameterExpression NewStateSlot(Type type)
        {
            var slot = Expression.Variable(type);
            Variables.Add(slot, null);
            return slot;
        }

        //async method cannot have block expression with type not equal to void
        protected override Expression VisitBlock(BlockExpression node)
        {
            if (node.Type == typeof(void))
            {
                node.Variables.ForEach(variable => Variables.Add(variable, null));
                return context.Rewrite(node, base.VisitBlock);
            }
            else
                throw new NotSupportedException("Async lambda cannot have block expression of type not equal to void");
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
        {
            if (node.Type == typeof(void))   //if statement
            {
                VisitorContext.MarkAsRewritePoint(node.IfTrue);
                VisitorContext.MarkAsRewritePoint(node.IfFalse);
            }
            else if (node.IfTrue is BlockExpression || node.IfFalse is BlockExpression)
                throw new NotSupportedException("A branch of conditional expression is invalid");
            return context.Rewrite(node, RewriteConditional);
        }

        protected override LabelTarget VisitLabelTarget(LabelTarget node)
            => node.Type == typeof(void) ? base.VisitLabelTarget(node) : throw new NotSupportedException("Label should be of type Void");

        protected override Expression VisitLambda<T>(Expression<T> node)
            => context.Rewrite(node, base.VisitLambda);

        protected override Expression VisitListInit(ListInitExpression node)
            => context.Rewrite(node, base.VisitListInit);

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
            => context.Rewrite(node, base.VisitTypeBinary);

        protected override Expression VisitUnary(UnaryExpression node)
            => context.Rewrite(node, base.VisitUnary);

        protected override Expression VisitSwitch(SwitchExpression node)
            => node.Type == typeof(void) ?
                context.Rewrite(node, base.VisitSwitch) :
                throw new InvalidOperationException("Switch-case expression must of type Void");

        protected override Expression VisitGoto(GotoExpression node)
            => node.Type == typeof(void) ?
                context.Rewrite(node, base.VisitGoto) :
                throw new InvalidOperationException("Goto expression should of type Void");

        protected override Expression VisitTry(TryExpression node)
        {
            if(node.Type == typeof(void))
                return context.Rewrite(node, base.VisitTry);
            throw new NotSupportedException("Try-Catch statement should be of type Void");
        }

        private Expression VisitAwait(AwaitExpression node)
        {
            node = (AwaitExpression)base.VisitExtension(node);
            context.ContainsAwait();
            //allocate slot for awaiter
            var awaiterSlot = NewStateSlot(node.AwaiterType);
            MarkAsAwaiterVar(awaiterSlot);
            //generate new state and label for it
            var state = NextState();
            var stateLabel = Expression.Label("state_" + state);
            stateSwitchTable[state] = stateLabel;
            //convert await expression into TAwaiter.GetResult() expression
            return node.Reduce(awaiterSlot, state, stateLabel, AsyncMethodEnd, context.GetStatementPrologueWriter());
        }

        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case AwaitExpression await:
                    return context.Rewrite(await, VisitAwait);
                case AsyncResultExpression ar:
                    return ar;
                case TransitionExpression transition:
                    return transition;
                default:
                    return context.Rewrite(node.Reduce(), Visit);
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

        internal BlockExpression BeginRewrite(Expression body)
        {
            var result = body is BlockExpression block ?
                Expression.Block(typeof(void), block.Variables, block.Expressions) :
                Expression.Block(typeof(void), body);
            return (BlockExpression)VisitBlock(result);
        }

        private static Expression GetStateId(ParameterExpression stateMachine)
            => stateMachine.Property(nameof(AsyncStateMachine<ValueTuple>.StateId));

        internal BlockExpression EndRewrite(ParameterExpression stateMachine, BlockExpression body)
        {
            var stateSwitch = MakeSwitch(GetStateId(stateMachine), stateSwitchTable);
            return Expression.Block(body.Type, body.Variables, 
                Sequence.Single(stateSwitch).Concat(body.Expressions).Concat(Sequence.Single(AsyncMethodEnd.LandingSite())));
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
        /*
         Try-catch-finally transformation:
            try
            {
                await A;
                B;
            }
            catch(Exception e)
            {
                await C;
                D;
            }
            finally
            {
                F;
            }

            transformed into
            begin:
            try
            {
                switch(state)
                {
                    case 1: goto state_1;
                    case 2: goto state_2;
                    case 3: goto catch_block;
                    case 4: goto exit_try;
                }
                awaiter1 = A;
                state = 1;
                return;
                state_1:
                awaiter1.GetResult();
                B;
                goto exit_try;
                //catch block
                catch_block:
                awaiter2 = C;
                state = 2;
                return;
                state_2:
                awaiter2.GetResult();
                D;
                exit_try:
                //finally block
                F;
                if(rethrowException != null)
                    rethrowException.Throw();
            }
            catch(Exception e)
            {
                switch(state)
                {
                    case 0: 
                    case 1: state = 3; goto begin;
                    case 2: state = 4; goto begin;
                }
                builder.SetException(e);
                goto end;
            }
            builder.SetResult(default(R));
            end:
         */
        
        private readonly AsyncStateMachineBuilder methodBuilder;
        private ParameterExpression stateMachine;

        private AsyncStateMachineBuilder(Expression<D> source)
        {
            methodBuilder = new AsyncStateMachineBuilder(source.ReturnType.GetTaskType(), source.Parameters);
        }

        private static Expression CreateFallbackResult(ParameterExpression stateMachine)
        {
            var resultProperty = stateMachine.Type.GetProperty(nameof(AsyncStateMachine<ValueTuple, int>.Result));
            if (!(resultProperty is null))
                return Expression.Property(stateMachine, resultProperty).Assign(resultProperty.PropertyType.Default());
            //just call Complete method, async method doesn't have return type
            return stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.Complete));
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
            newBody.Add(Expression.Call(null, stateMachine.Type.GetMethod("Start"), stateMachineMethod, stateVariable));
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
            => methodBuilder.Variables.TryGetValue(node, out var stateSlot) ? stateSlot : node.Upcast<Expression, ParameterExpression>();

        protected override Expression VisitBlock(BlockExpression node)
            => base.VisitBlock(Expression.Block(node.Expressions));

        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case TransitionExpression transition:
                    return Visit(transition.Reduce(stateMachine));
                case AsyncResultExpression result:
                    return Visit(result.Reduce(stateMachine, methodBuilder.AsyncMethodEnd));
                default:
                    return base.VisitExtension(node);
            }
        }

        private Expression<D> Build(BlockExpression body, bool tailCall)
        {
            //add state switch
            body = methodBuilder.EndRewrite(stateMachine, body);
            //now we have state machine method, wrap it into lambda
            return Build(BuildStateMachine(body, stateMachine, tailCall));
        }

        private Expression<D> Build(Expression body, bool tailCall)
        {
            body = methodBuilder.BeginRewrite(body);
            //build state machine type
            stateMachine = CreateStateHolderType(methodBuilder.AsyncReturnType, methodBuilder.Variables);
            //replace all transitions and async returns
            body = Visit(body);
            return Build((BlockExpression)body, tailCall);
        }

        internal static Expression<D> Build(Expression<D> source)
        {
            using (var builder = new AsyncStateMachineBuilder<D>(source))
            {
                return builder.Build(source.Body, source.TailCall) ?? source;
            }
        }

        public void Dispose()
        {
            methodBuilder.Dispose();
        }
    }
}
