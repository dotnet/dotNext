using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal sealed class StatePlaceholderExpression : Expression
    {
        private uint? stateId;

        internal StatePlaceholderExpression(uint stateId)
            => this.stateId = stateId;

        public StatePlaceholderExpression()
            => stateId = null;

        internal uint StateId
        {
            set => stateId = value;
        }

        public override bool CanReduce => stateId.HasValue;
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(uint);

        public override Expression Reduce()
        {
            Debug.Assert(stateId.HasValue);
            return Constant(stateId.Value);
        }
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
    }
}
