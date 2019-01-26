using System;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MethodInfo = System.Reflection.MethodInfo;

namespace DotNext.Runtime.CompilerServices
{
    using static Collections.Generic.Dictionaries;
    using Metaprogramming;
    using Reflection;
    using static Collections.Generic.Collections;

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
        internal readonly Type AsyncReturnType;
        //stored captured exception to re-throw
        internal readonly ParameterExpression CapturedException;
        private readonly ISet<ParameterExpression> variables;
        private int stateId;
        private readonly VisitorContext context;
        //this label indicates beginning of async method
        //should be placed before try
        private readonly LabelTarget asyncMethodBegin;
        //this label indicates end of async method when successful result should be returned
        private readonly LabelTarget asyncMethodEnd;
         //a table with labels and how to handle exceptions
        private readonly IDictionary<int, LabelTarget> exceptionSwitchTable;
        //a table with labels in the beginning of async state machine
        private readonly IDictionary<int, LabelTarget> stateSwitchTable;

        internal AsyncStateMachineBuilder(Type returnType)
        {
            if (returnType is null)
                throw new ArgumentException("Invalid return type of async method");
            AsyncReturnType = returnType;
            CapturedException = Expression.Variable(typeof(ExceptionDispatchInfo));
            variables = new HashSet<ParameterExpression>() { CapturedException };
            stateId = AsyncStateMachine<ValueTuple>.INITIAL_STATE;
            context = new VisitorContext();
            asyncMethodBegin = Expression.Label("begin_async_method");
            asyncMethodEnd = Expression.Label("end_async_method");
        }

        private int NextState() => ++stateId;
        

        //async method cannot have block expression with type not equal to void
        protected override Expression VisitBlock(BlockExpression node)
            => node.Type == typeof(void) ? 
                context.Rewrite(node, base.VisitBlock) : 
                throw new NotSupportedException("Async lambda cannot have block expression of type not equal to void");

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if(node.Type == typeof(void))   //if statement
            {
                VisitorContext.MarkAsRewritePoint(node.IfTrue);
                VisitorContext.MarkAsRewritePoint(node.IfFalse);
            }
            else if (node.IfTrue is BlockExpression || node.IfFalse is BlockExpression)
                throw new NotSupportedException("A branch of conditional expression is invalid");
            var result = context.Rewrite(node, base.VisitConditional);
            if(result is ConditionalExpression)
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
            if(VisitorContext.ContainsAwait(node.IfTrue) || VisitorContext.ContainsAwait(node.IfFalse))
            {
                var tempVar = Expression.Variable(node.Type);
                variables.Add(tempVar);
                context.GetCodeInsertionPoint().Invoke(Expression.Condition(node.Test, Expression.Assign(tempVar, node.IfTrue), Expression.Assign(tempVar, node.IfFalse), typeof(void)));
                return tempVar;
            }
            else
                return node;
        }

        protected override LabelTarget VisitLabelTarget(LabelTarget node)
            => node.Type == typeof(void) ? base.VisitLabelTarget(node) : throw new NotSupportedException("Label should be of type Void");

        protected override Expression VisitTry(TryExpression node)
        {
            if(node.Type == typeof(void))
                return context.Rewrite(node, base.VisitTry);
            throw new NotSupportedException("Try-Catch statement should be of type Void");
        }

        //detect local variable which will be replaced with state slot
        protected override Expression VisitParameter(ParameterExpression node)
        {
            variables.Add(node);
            return node;
        }

        private Expression VisitAwait(AwaitExpression node)
        {
            node = (AwaitExpression)base.VisitExtension(node);
            context.ContainsAwait();
            //allocate slot for awaiter
            var awaiterSlot = Expression.Variable(node.AwaiterType);
            variables.Add(awaiterSlot);
            //generate new state and label for it
            var state = NextState();
            var stateLabel = Expression.Label("state_" + state);
            stateSwitchTable[state] = stateLabel;
            //convert await expression into TAwaiter.GetResult() expression
            return node.Reduce(awaiterSlot, state, stateLabel, asyncMethodEnd, context.GetCodeInsertionPoint());
        }

        protected override Expression VisitExtension(Expression node)
            => node is AwaitExpression await ? context.Rewrite(await, VisitAwait) : context.Rewrite(node, base.VisitExtension);

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

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var codeInsertionPoint = context.GetCodeInsertionPoint();
            var newNode = context.Rewrite(node, base.VisitBinary);
            if(newNode is BinaryExpression)
                node = (BinaryExpression)newNode;
            else
                return newNode;
            //do not place left operand at statement level because it has no side effects
            if(node.Left is ParameterExpression || node.Left is ConstantExpression || IsAssignment(node))
                return node;
            var leftIsAsync = VisitorContext.ContainsAwait(node.Left);
            var rightIsAsync = VisitorContext.ContainsAwait(node.Right);
            //left operand should be computed before right, so bump it before await expression
            if(rightIsAsync && !leftIsAsync)
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
                var leftTemp = Expression.Variable(node.Left.Type);
                variables.Add(leftTemp);
                codeInsertionPoint(Expression.Assign(leftTemp, node.Left));
                node = node.Update(leftTemp, node.Conversion, node.Right);
            }
            return node;
        }

        private Expression VisitCallable<E>(E node, Expression[] arguments, Converter<E, Expression> visitor, Func<E, Expression[], E> updater)
            where E: Expression
        {
            var codeInsertionPoint = context.GetCodeInsertionPoint();
            var newNode = context.Rewrite(node, visitor);
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
                    var tempVar = Expression.Variable(arg.Type);
                    codeInsertionPoint(Expression.Assign(tempVar, arg));
                    arg = tempVar;
                }
            }
            return updater(node, arguments);
        }

        private static MethodCallExpression UpdateArguments(MethodCallExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(node.Object, arguments);

        protected override Expression VisitMethodCall(MethodCallExpression node)
            => VisitCallable(node, node.Arguments.ToArray(), base.VisitMethodCall, UpdateArguments);

        private static InvocationExpression UpdateArguments(InvocationExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(node.Expression, arguments);

        protected override Expression VisitInvocation(InvocationExpression node)
            => VisitCallable(node, node.Arguments.ToArray(), base.VisitInvocation, UpdateArguments);
        
        private static IndexExpression UpdateArguments(IndexExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(node.Object, arguments);

        protected override Expression VisitIndex(IndexExpression node)
            => VisitCallable(node, node.Arguments.ToArray(), base.VisitIndex, UpdateArguments);
        
        private static NewExpression UpdateArguments(NewExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(arguments);

        protected override Expression VisitNew(NewExpression node)
            => VisitCallable(node, node.Arguments.ToArray(), base.VisitNew, UpdateArguments);
        
        private static NewArrayExpression UpdateArguments(NewArrayExpression node, IReadOnlyCollection<Expression> arguments)
            => node.Update(arguments);

        protected override Expression VisitNewArray(NewArrayExpression node)
            => VisitCallable(node, node.Expressions.ToArray(), base.VisitNewArray, UpdateArguments);

        internal static MemberExpression GetStateField(ParameterExpression stateMachine)
            => stateMachine.Field(nameof(AsyncStateMachine<int>.State));

        public void Dispose()
        {
            variables.Clear();
            exceptionSwitchTable.Clear();
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
        //this label indicates beginning of async method
        //should be placed before try
        private readonly LabelTarget asyncMethodBegin;
        //this label indicates end of async method when successful result should be returned
        private readonly LabelTarget asyncMethodEnd;
        //body of async method inside of try section
        private readonly ICollection<Expression> tryBlock;
        //a table with labels and how to handle exceptions
        private readonly IDictionary<int, LabelTarget> exceptionSwitchTable;
        //a table with labels in the beginning of async state machine
        private readonly IDictionary<int, LabelTarget> stateSwitchTable;
        private int stateId;

        private AsyncStateMachineBuilder(Expression<D> source)
        {
            methodBuilder = new AsyncStateMachineBuilder(source.ReturnType.GetTaskType());
            asyncMethodBegin = Expression.Label("begin_async_method");
            asyncMethodEnd = Expression.Label("end_async_method");
            tryBlock = new LinkedList<Expression>();
            exceptionSwitchTable = new Dictionary<int, LabelTarget>();
            stateSwitchTable = new Dictionary<int, LabelTarget>();           
            stateId = AsyncStateMachine<ValueTuple>.INITIAL_STATE;
        }

        
        //replace every local variable with appropriate state slot
        protected override Expression VisitParameter(ParameterExpression node)
        {
            var slot = methodBuilder[node];
            return slot is null ? base.VisitParameter(node) : VisitMember(slot);
        }

        //async method cannot have block expression with type not equal to void
        protected override Expression VisitBlock(BlockExpression node)
        {
            return node.Type == typeof(void) ? base.VisitBlock(node) : throw new NotSupportedException("Async lambda cannot have block expression of type not equal to void");
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if (node.Type != typeof(void) && (node.IfTrue is BlockExpression || node.IfFalse is BlockExpression))
                throw new NotSupportedException("A branch of conditional expression is invalid");
            return base.VisitConditional(node);
        }

        protected override LabelTarget VisitLabelTarget(LabelTarget node)
            => node.Type == typeof(void) ? base.VisitLabelTarget(node) : throw new NotSupportedException("Label should be of type Void");

        protected override Expression VisitTry(TryExpression node)
            => node.Type == typeof(void) ? base.VisitTry(node) : throw new NotSupportedException("Try-Catch statement should be of type Void");

        protected override Expression VisitExtension(Expression node)
        {
            if (node is AsyncResultExpression result)
                node = result.Reduce(stateMachine, asyncMethodEnd);
            else if (node is AwaitExpression await)
            {
                var state = NextState();
                var stateLabel = Expression.Label("state_" + state);
                stateSwitchTable[state] = stateLabel;
                node = await.Reduce(stateMachine, methodBuilder[await], state, stateLabel, asyncMethodEnd);
            }
            else
                return Visit(node.Reduce());
            return Visit(node);
        }

        private static Expression CreateFallbackResult(ParameterExpression stateMachine)
        {
            var resultProperty = stateMachine.Type.GetProperty(nameof(AsyncStateMachine<ValueTuple, int>.Result));
            if (!(resultProperty is null))
                return Expression.Property(stateMachine, resultProperty).Assign(resultProperty.PropertyType.Default());
            //just call Complete method, async method doesn't have return type
            return stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.Complete));
        }

        private static LambdaExpression BuildStateMachine(Expression body, ParameterExpression stateMachine)
        {
            var delegateType = stateMachine.Type.GetNestedType(nameof(AsyncStateMachine<ValueTuple>.Transition));
            delegateType = delegateType.MakeGenericType(stateMachine.Type.GetGenericArguments());
            return Expression.Lambda(delegateType, body, stateMachine);
        }

        private static Expression<D> Build(LambdaExpression stateMachineMethod, Type stateMachineType, IReadOnlyCollection<ParameterExpression> parameters, Func<ParameterExpression, MemberExpression> parameterMapper)
        {
            var stateMachine = Expression.Variable(stateMachineType, "stateMachine");
            //save all parameters into fields
            ICollection<Expression> newBody = new LinkedList<Expression>();
            //create new state machine (new AsyncStateMachine<InferredStateType, ResultType>)
            var newStateMachine = Expression.New(stateMachineType.GetConstructor(new[] { stateMachineMethod.Type }), stateMachineMethod);
            newBody.Add(Expression.Assign(stateMachine, newStateMachine));
            foreach (var parameter in parameters)
                newBody.Add(parameterMapper(parameter).Update(AsyncStateMachineBuilder.GetStateField(stateMachine)).Assign(parameter));
            newBody.Add(stateMachine.Call(nameof(IAsyncStateMachine<ValueTuple>.Start)));
            return Expression.Lambda<D>(Expression.Block(new[] { stateMachine }, newBody), parameters);
        }

        private static SwitchExpression MakeSwitch(Expression stateId, IDictionary<int, LabelTarget> switchTable)
        {
            ICollection<SwitchCase> cases = new LinkedList<SwitchCase>();
            foreach (var (state, label) in switchTable)
                cases.Add(Expression.SwitchCase(label.Goto(), state.AsConst()));
            return Expression.Switch(stateId, Expression.Empty(), null, cases);
        }

        private Expression<D> Build(Expression body, IReadOnlyCollection<ParameterExpression> parameters)
        {
            //transformation stage #1 - replace all local variables
            stateMachine = methodBuilder.Initialize(parameters, ref body);
            if (stateMachine is null)
                return null;
            //transformation stage #2 - replace await/async expression with well-known alternatives
            body = Visit(body);
            //transformation stage #3 - construct state machine method
            //build result fallback
            var fallback = CreateFallbackResult(stateMachine);
            //set exception
            var stateMachineException = Expression.Variable(typeof(Exception), "e");
            var setException = Expression.Assign(stateMachine.Property(nameof(IAsyncStateMachine<ValueTuple>.Exception)), stateMachineException);
            //state field
            var stateId = stateMachine.Property(nameof(IAsyncStateMachine<ValueTuple>.StateId));
            //construct body inside of try block
            if (body is BlockExpression block)
            {
                body = MakeSwitch(stateId, stateSwitchTable);
                body = Expression.Block(typeof(void), block.Variables, Sequence.Single(body).Concat(block.Expressions));
            }
            else
                body = Expression.Block(typeof(void), MakeSwitch(stateId, stateSwitchTable), body);
            //stage 4 - replace block expressions with statements
            body = BlockSimplifier.Simplify((BlockExpression)body);
            //construct body inside of catch block
            var stateMachineCatch = Expression.Catch(stateMachineException,
                        exceptionSwitchTable.Count == 0 ?
                            Expression.Block(typeof(void), setException) :
                            Expression.Block(typeof(void), MakeSwitch(stateId, exceptionSwitchTable), setException)
                        );
            //all together
            body = Expression.Block(
                asyncMethodBegin.LandingSite(),
                Expression.TryCatch(body, stateMachineCatch),
                fallback,
                asyncMethodEnd.LandingSite());
            //now we have state machine method, wrap it into lambda
            return Build(BuildStateMachine(body, stateMachine), stateMachine.Type, parameters, methodBuilder.ParameterToMember);
        }

        internal static Expression<D> Build(Expression<D> source)
        {
            using (var builder = new AsyncStateMachineBuilder<D>(source))
            {
                return builder.Build(source.Body, source.Parameters) ?? source;
            }
        }

        public void Dispose()
        {
            methodBuilder.Dispose();
            tryBlock.Clear();
            exceptionSwitchTable.Clear();
            stateSwitchTable.Clear();
            stateMachine = null;
        }
    }
}
