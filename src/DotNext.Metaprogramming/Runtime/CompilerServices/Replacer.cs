using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal sealed class Replacer : ExpressionVisitor
    {
        private readonly IDictionary<Expression, Expression> replacement;

        internal Replacer()
        {
            replacement = new Dictionary<Expression, Expression>();
        }

        internal void Replace(Expression expected, Expression actual)
            => replacement.Add(expected, actual);

        public override Expression Visit(Expression node)
        {
            if (!(node is null) && replacement.TryGetValue(node, out var newNode))
                node = newNode;
            return base.Visit(node);
        }
    }
}
