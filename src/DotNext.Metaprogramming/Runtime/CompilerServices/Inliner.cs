using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal sealed class Inliner : ExpressionVisitor, IDisposable
    {
        private readonly IDictionary<LabelTarget, LabelTarget> labelReplacement;

        private Inliner()
        {
            labelReplacement = new Dictionary<LabelTarget, LabelTarget>();
        }

        protected override LabelTarget? VisitLabelTarget(LabelTarget? node)
        {
            LabelTarget? targetCopy;
            if (node is null)
            {
                targetCopy = null;
            }
            else if (!labelReplacement.TryGetValue(node, out targetCopy))
            {
                targetCopy = Expression.Label(node.Type, node.Name);
                labelReplacement.Add(node, targetCopy);
            }

            return targetCopy;
        }

        void IDisposable.Dispose() => labelReplacement.Clear();

        internal static Expression Rewrite(Expression node)
        {
            using var rewriter = new Inliner();
            return rewriter.Visit(node);
        }
    }
}
