using System;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;
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
        private sealed class AsyncStateMachineType
        {
            private ParameterExpression stateMachine;
            private readonly Type returnType;

            internal AsyncStateMachineType(Type returnType)
                => this.returnType = returnType;

            public static implicit operator ParameterExpression(AsyncStateMachineType type)
                => type?.stateMachine;

            internal MemberExpression MakeStateHolder(Type stateType)
            {
                var stateMachineType = returnType == typeof(void) ?
                    typeof(AsyncStateMachine<>).MakeGenericType(stateType) :
                    typeof(AsyncStateMachine<,>).MakeGenericType(stateType, returnType);
                stateMachineType = stateMachineType.MakeByRefType();
                stateMachine = Expression.Parameter(stateMachineType);
                return GetStateField(stateMachine);
            }
        }

        /// <summary>
        /// Represents state slot of state machine.
        /// </summary>
        /// <remarks>
        /// Slot is a representation of local variable declared
        /// in async method which value persists between
        /// different states.
        /// </remarks>
        private interface ISlot
        {
            /// <summary>
            /// Type of slot.
            /// </summary>
            Type Type { get; }
        }

        /// <summary>
        /// Represents local variable converted into state machine slot.
        /// </summary>
        private readonly struct VariableSlot : ISlot, IEquatable<VariableSlot>
        {
            private readonly ParameterExpression variable;

            private VariableSlot(ParameterExpression variable)
                => this.variable = variable;

            public static implicit operator VariableSlot(ParameterExpression variable)
                => new VariableSlot(variable);

            Type ISlot.Type => variable.Type;

            public bool Equals(VariableSlot other) => Equals(variable, other.variable);
            public override int GetHashCode() => variable.GetHashCode();
            public override bool Equals(object other)
            {
                switch (other)
                {
                    case ParameterExpression variable:
                        return Equals(variable, this.variable);
                    case VariableSlot slot:
                        return Equals(slot);
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Represents awaiter object holder.
        /// </summary>
        /// <remarks>
        /// This slot used to save awaiters from other asynchronous methods
        /// (returned by GetAwaiter method). If two different async method
        /// calls return the same type of awaiter, then slot will be reused
        /// to keep state small as possible.
        /// </remarks>
        private readonly struct AwaiterSlot : ISlot, IEquatable<AwaiterSlot>
        {
            private readonly Type awaiterType;

            private AwaiterSlot(AwaitExpression expression)
                => awaiterType = expression.AwaiterType;

            public static implicit operator AwaiterSlot(AwaitExpression variable)
                => new AwaiterSlot(variable);

            Type ISlot.Type => awaiterType;

            public bool Equals(AwaiterSlot other) => Equals(awaiterType, other.awaiterType);
            public override int GetHashCode() => awaiterType.GetHashCode();
            public override bool Equals(object other)
            {
                switch (other)
                {
                    case Type awaiterType:
                        return Equals(awaiterType, this.awaiterType);
                    case AwaiterSlot slot:
                        return Equals(slot);
                    default:
                        return false;
                }
            }
        }

        internal readonly Type AsyncReturnType;
        //stored captured exception to re-throw
        private readonly ParameterExpression capturedException;
        private readonly IDictionary<ISlot, MemberExpression> variables;
        //indicates that lambda body has at least one async call
        private bool hasNestedAsyncCalls = false;

        internal AsyncStateMachineBuilder(Type returnType)
        {
            if(returnType is null)
                throw new ArgumentException("Invalid return type of async method");
            variables = new Dictionary<ISlot, MemberExpression>();
            AsyncReturnType = returnType;
            capturedException = Expression.Variable(typeof(ExceptionDispatchInfo));
            variables.Add((VariableSlot)capturedException, null);
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            if (node.Variables.Count == 0)
                return base.VisitBlock(node);
            //replace every local variable with state slot
            foreach (var variable in node.Variables)
                variables[(VariableSlot)variable] = null; //field is uknown at this moment
            //now block can be replaced with its empty body
            Expression result;
            switch (node.Expressions.Count)
            {
                case 0:
                    result = Expression.Empty();
                    break;
                case 1:
                    result = node.Result;
                    break;
                default:
                    result = Expression.Block(node.Type, node.Expressions);
                    break;
            }
            return base.Visit(result);
        }

        public override Expression Visit(Expression node)
        {
            //allocate field to store awaitor only if it returns non-void value
            if (node is AwaitExpression await)
            {
                hasNestedAsyncCalls = true;
                variables[(AwaiterSlot)await] = null; //field is unknown at this moment
            }
            return base.Visit(node);
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
        private sealed class Replacement : Expression
        {
            private readonly Expression expression;

            internal Replacement(Expression expression)
            {

            }

            public override Type Type => expression.Type;
            public override bool CanReduce => true;
            public override Expression Reduce() => expression;
            public override ExpressionType NodeType => ExpressionType.Extension;
            protected override Expression VisitChildren(ExpressionVisitor visitor)
            {
                var expression = visitor.Visit(this.expression);
                return ReferenceEquals(this.expression, expression) ? this : new Replacement(expression);
            }
        }
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
        //lexical scope stack
        private readonly Stack<Expression> walkingStack;

        private AsyncStateMachineBuilder(Expression<D> source)
        {
            methodBuilder = new AsyncStateMachineBuilder(source.ReturnType.GetTaskType());
            asyncMethodBegin = Expression.Label("begin_async_method");
            asyncMethodEnd = Expression.Label("end_async_method");
            tryBlock = new LinkedList<Expression>();
            exceptionSwitchTable = new Dictionary<int, LabelTarget>();
            stateSwitchTable = new Dictionary<int, LabelTarget>();           
            stateId = AsyncStateMachine<ValueTuple>.INITIAL_STATE;
            walkingStack = new Stack<Expression>();
        }

        private int NextState() => ++stateId;

        private Expression Visit<E>(E expression, Func<E, Expression> visitor)
            where E: Expression
        {
            walkingStack.Push(expression);
            try
            {
                return visitor(expression);
            }
            finally
            {
                walkingStack.Pop();
            }
        }

        //replace every local variable with appropriate state slot
        protected override Expression VisitParameter(ParameterExpression node)
        {
            var slot = methodBuilder[node];
            return slot is null ? Visit(node, base.VisitParameter) : Visit(slot, VisitMember);
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            var parent = walkingStack.Count > 0 ? walkingStack.Peek() : null;
            //flatten block expression
            if (parent is null || parent is BlockExpression || node.Expressions.Count < 2)
                return Visit(node, base.VisitBlock);
            else
            {
                node.Expressions.Last
            }
        }

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
                return Visit(node, base.VisitExtension);
            return Visit(node, Visit);
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

        private Expression<D> Build(Expression body, IReadOnlyCollection<ParameterExpression> parameters)
        {
            //transformation stage #1 - replace all local variables
            stateMachine = methodBuilder.Initialize(parameters, ref body);
            if (stateMachine is null)
                return null;
            //transformation stage #2 - transform block expressions into statements and replace 
            //await /async expression into well-known alternatives
            body = Visit(body);
            //transformation stage #3 - construct state machine method
            //build switch table
            ICollection<SwitchCase> stateSwitchTable = new LinkedList<SwitchCase>();
            foreach (var (state, label) in this.stateSwitchTable)
                stateSwitchTable.Add(Expression.SwitchCase(label.Goto(), state.AsConst()));
            //build exception handling table
            ICollection<SwitchCase> exceptionSwitchTable = new LinkedList<SwitchCase>();
            foreach (var (state, label) in this.exceptionSwitchTable)
                exceptionSwitchTable.Add(Expression.SwitchCase(label.Goto(), state.AsConst()));
            //build result fallback
            var fallback = CreateFallbackResult(stateMachine);
            //set exception
            var stateMachineException = Expression.Variable(typeof(Exception), "e");
            var setException = Expression.Assign(stateMachine.Property(nameof(IAsyncStateMachine<ValueTuple>.Exception)), stateMachineException);
            //state field
            var stateId = stateMachine.Property(nameof(IAsyncStateMachine<ValueTuple>.StateId));
            //construct body inside of try block
            body = Expression.Block(typeof(void), Expression.Switch(stateId, Expression.Empty(), null, stateSwitchTable), body);
            //construct body inside of catch block
            var stateMachineCatch = Expression.Catch(stateMachineException,
                        exceptionSwitchTable.Count == 0 ?
                            Expression.Block(typeof(void), setException) :
                            Expression.Block(typeof(void), Expression.Switch(stateId, Expression.Empty(), null, exceptionSwitchTable), setException)
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
