using System;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using Metaprogramming;
    using Reflection;
    using static Collections.Generic.Collections;

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
                stateMachine = Expression.Parameter(returnType == typeof(void) ?
                    typeof(AsyncStateMachine<>).MakeGenericType(stateType) :
                    typeof(AsyncStateMachine<,>).MakeGenericType(stateType, returnType));
                return stateMachine.Field(nameof(AsyncStateMachine<int>.State));
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
        //a set of variables which are not propagated as state slots
        private readonly ISet<ParameterExpression> ignoredVariables;
        //indicates that lambda body has at least one async call
        private bool hasNestedAsyncCalls = false;

        internal AsyncStateMachineBuilder(Type returnType)
        {
            if(returnType is null)
                throw new ArgumentException("Invalid return type of async method");
            variables = new Dictionary<ISlot, MemberExpression>();
            ignoredVariables = new HashSet<ParameterExpression>();
            AsyncReturnType = returnType;
            capturedException = Expression.Variable(typeof(ExceptionDispatchInfo));
            variables.Add((VariableSlot)capturedException, null);
        }

        protected override Expression VisitParameter(ParameterExpression variable)
        {
            if (!ignoredVariables.Contains(variable))
                variables[(VariableSlot)variable] = null; //field is uknown at this moment
            return base.VisitParameter(variable);
        }

        protected override Expression VisitTry(TryExpression node)
        {
            //exception variables should not be placed as state slots
            foreach (var @catch in node.Handlers)
                if (!(@catch.Variable is null))
                    ignoredVariables.Add(@catch.Variable);
            return base.VisitTry(node);
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

        internal ParameterExpression Initialize(Expression root)
        {
            Visit(root);
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

        /// <summary>
        /// Returns state storage slot for the specified local variable.
        /// </summary>
        /// <param name="variable">Local variable.</param>
        /// <returns>A field access expression.</returns>
        internal MemberExpression this[ParameterExpression variable]
            => variables[(VariableSlot)variable];

        /// <summary>
        /// Returns state storage slot for the async result. 
        /// </summary>
        /// <param name="awaiterType">Awaiter type.</param>
        /// <returns></returns>
        internal MemberExpression this[AwaitExpression awaiterType]
            => variables[(AwaiterSlot)awaiterType];

        public void Dispose()
        {
            variables.Clear();
            ignoredVariables.Clear();
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
            }
            builder.SetResult(default(R));
            end:
         */
        private readonly AsyncStateMachineBuilder methodBuilder;
        private readonly ParameterExpression stateMachine;
        //this label indicates beginning of async method
        //should be placed before try
        private readonly LabelTarget asyncMethodBegin;
        //this label indicates end of async method
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
            stateMachine = methodBuilder.Initialize(source.Body);
            asyncMethodBegin = Expression.Label("begin_async_method");
            asyncMethodEnd = Expression.Label("end_async_method");
            tryBlock = new LinkedList<Expression>();
            exceptionSwitchTable = new Dictionary<int, LabelTarget>();
            stateSwitchTable = new Dictionary<int, LabelTarget>();
            stateId = AsyncStateMachine<ValueTuple>.INITIAL_STATE;
        }

        private int NextState() => ++stateId;

        private Expression<D> Build(Expression body, IReadOnlyCollection<ParameterExpression> parameters)
        {
            if (stateMachine is null)
                return null;
            
            return null;
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
        }
    }
}
