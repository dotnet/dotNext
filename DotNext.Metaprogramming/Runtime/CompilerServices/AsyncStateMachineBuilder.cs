using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using Metaprogramming;
    using Reflection;
    using static Collections.Generic.Collections;

    internal sealed class AsyncStateMachineBuilder : ExpressionVisitor
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

        private readonly Type asyncReturnType;
        private readonly IDictionary<ISlot, MemberExpression> variables;
        //indicates that lambda body has at least one async call
        private bool hasNestedAsyncCalls = false;

        internal AsyncStateMachineBuilder(Type returnType)
        {
            variables = new Dictionary<ISlot, MemberExpression>();
            asyncReturnType = returnType;
        }

        protected override Expression VisitParameter(ParameterExpression variable)
        {
            variables[(VariableSlot)variable] = null; //field is uknown at this moment
            return variable;
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
                    var type = new AsyncStateMachineType(asyncReturnType);
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
    }

    internal sealed class AsyncStateMachineBuilder<D>: ExpressionVisitor
        where D: Delegate
    {
        private readonly Type returnType;

        private AsyncStateMachineBuilder(Type asyncReturnType)
        {
            if (asyncReturnType is null)
                throw new ArgumentException("Invalid return type of async method");
            returnType = asyncReturnType;
        }

        private static BlockExpression BuildSynchronousVoidLambda(Expression body)
        {
            return null;
        }

        private Expression<D> Build(Expression body, IReadOnlyCollection<ParameterExpression> parameters)
        {
            var builder = new AsyncStateMachineBuilder(returnType);
            var stateMachine = builder.Initialize(body);
            if (stateMachine is null)
                return null;
            return null;
        }

        internal static Expression<D> Build(Expression<D> source)
        {
            var builder = new AsyncStateMachineBuilder<D>(source.ReturnType.GetTaskType());
            return builder.Build(source.Body, source.Parameters) ?? source;
        }
    }
}
