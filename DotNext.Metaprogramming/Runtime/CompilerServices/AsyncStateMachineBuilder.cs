using System;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

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
        private sealed class Replacement
        {
            private readonly ICollection<Expression> expressions;
            private readonly ICollection<ParameterExpression> variables;

            internal Replacement()
            {
                expressions = new LinkedList<Expression>();
                variables = new LinkedList<ParameterExpression>();
            }

            internal Expression Rewrite(Expression expression)
                => variables.Count == 0 && expressions.Count == 0 ? expression : Expression.Block(typeof(void), variables, expressions.Concat(Sequence.Single(expression)));
        }

        private static readonly UserDataSlot<Replacement> StatementSlot = UserDataSlot<Replacement>.Allocate();
        internal readonly Type AsyncReturnType;
        //stored captured exception to re-throw
        private readonly ParameterExpression capturedException;
        private readonly ISet<ParameterExpression> variables;
        private int stateId;
        private readonly Stack<Expression> walkingStack;

        internal AsyncStateMachineBuilder(Type returnType)
        {
            if (returnType is null)
                throw new ArgumentException("Invalid return type of async method");
            AsyncReturnType = returnType;
            capturedException = Expression.Variable(typeof(ExceptionDispatchInfo));
            variables = new HashSet<ParameterExpression>() { capturedException };
            stateId = AsyncStateMachine<ValueTuple>.INITIAL_STATE;
            walkingStack = new Stack<Expression>();
        }

        private Replacement FindReplacement()
        {
            foreach (var lookup in walkingStack)
            {
                if (lookup.GetUserData().Get(StatementSlot))
                    }
        }

        private int NextState() => ++stateId;

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

        //detect local variable which will be replaced with state slot
        protected override Expression VisitParameter(ParameterExpression node)
        {
            variables.Add(node);
            return base.VisitParameter(node);
        }

        private Expression VisitAwait(AwaitExpression node)
        {
            //allocate slot for awaiter
            var awaiterSlot = Expression.Variable(node.AwaiterType);
            variables.Add(awaiterSlot);
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is AwaitExpression await)
                return VisitAwait(await);
            return Visit(node.Reduce());
        }

        internal ParameterExpression Initialize(IEnumerable<ParameterExpression> parameters, ref Expression root)
        {
            foreach (var parameter in parameters)
                variables[(VariableSlot)parameter] = null;

            root = Visit(root);
            if (hasNestedAsyncCalls)
            {
                var variables = this.variables.Keys.ToArray();
                //construct value type
                MemberExpression[] fields;
                ParameterExpression stateMachine;
                using (var builder = new ValueTupleBuilder())
                {
                    variables.ForEach(slot => builder.Add(slot.Type));
                    //discover slots and build state machine type
                    var type = new AsyncStateMachineType(AsyncReturnType);
                    fields = builder.Build(type.MakeStateHolder, out _);
                    for (var i = 0L; i < fields.LongLength; i++)
                        this.variables[variables[i]] = fields[i];
                    stateMachine = type;
                }
                return stateMachine;
            }
            else
                return null;
        }

        /// <summary>
        /// Gets state storage slot for the captured exception.
        /// </summary>
        internal MemberExpression CapturedException => this[capturedException];

        internal MemberExpression ParameterToMember(ParameterExpression variable)
            => variables.TryGetValue((VariableSlot)variable, out var result) ? result : null;

        /// <summary>
        /// Returns state storage slot for the specified local variable.
        /// </summary>
        /// <param name="variable">Local variable.</param>
        /// <returns>A field access expression.</returns>
        internal MemberExpression this[ParameterExpression variable]
            => ParameterToMember(variable);

        /// <summary>
        /// Returns state storage slot for the async result. 
        /// </summary>
        /// <param name="awaiterType">Awaiter type.</param>
        /// <returns></returns>
        internal MemberExpression this[AwaitExpression awaiterType]
            => variables.TryGetValue((AwaiterSlot)awaiterType, out var result) ? result : null;

        internal static MemberExpression GetStateField(ParameterExpression stateMachine)
            => stateMachine.Field(nameof(AsyncStateMachine<int>.State));

        public void Dispose()
        {
            variables.Clear();
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
