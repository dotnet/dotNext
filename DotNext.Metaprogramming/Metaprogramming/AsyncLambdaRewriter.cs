using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Runtime.CompilerServices;
    using static Collections.Generic.Collections;

    internal sealed class StateFieldAllocator: ExpressionVisitor
    {
        private interface ISlot
        {
            Type Type { get; }
        }

        private readonly struct VariableSlot: ISlot, IEquatable<VariableSlot>
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
                switch(other)
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

        private readonly struct AwaiterSlot: ISlot, IEquatable<AwaiterSlot>
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

        private readonly IDictionary<VariableSlot, MemberExpression> variables;
        private readonly IDictionary<AwaiterSlot, MemberExpression> awaiters;
        //indicates that lambda body has at least one async call
        private bool hasNestedAsyncCalls = false;

        internal StateFieldAllocator()
        {
            variables = new Dictionary<VariableSlot, MemberExpression>();
            awaiters = new Dictionary<AwaiterSlot, MemberExpression>();
        }

        protected override Expression VisitParameter(ParameterExpression variable)
        {
            variables[variable] = null; //field is uknown at this moment
            return variable;
        }

        public override Expression Visit(Expression node)
        {
            //allocate field to store awaitor only if it returns non-void value
            if (node is AwaitExpression await)
            {
                hasNestedAsyncCalls = true;
                if (await.Type != typeof(void))
                    awaiters[await] = null; //field is unknown at this moment
            }
            return base.Visit(node);
        }

        private static MemberExpression GetTupleField(MemberExpression parent, Type tupleType, int fieldNumber)
        {
            tupleType.GetField("Item1" + fieldNumber)
        }

        private static ParameterExpression AllocateStateStruct<S>(S[] slots, IDictionary<S, MemberExpression> output)
            where S: ISlot
        {
            //construct value type
            var builder = new ValueTupleBuilder();
            slots.ForEach(slot => builder.Add(slot.Type));
            //discover slots
            var fields = builder.BuildFields(Expression.Parameter, out var stateHolder);
            for (var i = 0L; i < fields.LongLength; i++)
                output[slots[i]] = fields[i];
            return stateHolder;
        }

        internal bool Allocate(Expression root)
        {
            Visit(root);
            if (hasNestedAsyncCalls)
            {
                var variables = this.variables.Keys.ToArray();
                var awaiters = this.awaiters.Keys.ToArray();
                //construct separated type for awaiters

                return true;
            }
            else
                return false;
        }
    }

    internal sealed class AsyncBodyRewriter: ExpressionVisitor
    {
        /// <summary>
        /// Represents local variable replaced with state slot
        /// inside of state machine.
        /// </summary>
        private sealed class StateFieldExpression : Expression
        {
            private Expression stateHolder;
            private FieldInfo field;

            internal StateFieldExpression(ParameterExpression replacedVar)
            {
                Type = replacedVar.Type;
            }

            internal void SetField(Expression stateHolder, FieldInfo field)
            {
                this.field = field;
                this.stateHolder = stateHolder;
            }

            /// <summary>
            /// Gets name of the field.
            /// </summary>
            internal string Name { get; }
            public override bool CanReduce => true;
            public override ExpressionType NodeType => ExpressionType.Extension;
            public override Type Type { get; }
            public override Expression Reduce() => Field(stateHolder, field);
        }

        private readonly IDictionary<ParameterExpression, StateFieldExpression> variables;

        internal AsyncBodyRewriter(IEnumerable<ParameterExpression> variables)
        {
            this.variables = new Dictionary<ParameterExpression, StateFieldExpression>();
            foreach (var variable in variables)
                this.variables.Add(variable, new StateFieldExpression(variable));
        }

        internal AsyncBodyRewriter()
            : this(Array.Empty<ParameterExpression>())
        {

        }

        //replace every local variable with a field representing state of async operation
        protected override Expression VisitParameter(ParameterExpression variable)
        {
            if (!variables.TryGetValue(variable, out var field))
            {
                field = new StateFieldExpression(variable);
                variables.Add(variable, field);
            }
            return field;
        }
    }

    internal sealed class AsyncLambdaRewriter<D>: ExpressionVisitor
        where D: Delegate
    {
        internal static Expression<D> Rewrite(Expression<D> source)
        {
            var rewriter = new AsyncBodyRewriter(source.Parameters);
            rewriter.Visit(source.Body);
            return null;
        }
    }
}
