using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using static System.Linq.Enumerable;

namespace DotNext.Runtime.CompilerServices
{
    using VariantType;

    internal sealed class VisitorContext : Stack<Expression>
    {
        private sealed class CodeInsertionPoint
        {
            private Variant<LinkedList<Expression>, LinkedListNode<Expression>> nodeOrList;

            internal CodeInsertionPoint(LinkedList<Expression> list) => nodeOrList = list;
            internal CodeInsertionPoint(LinkedListNode<Expression> node) => nodeOrList = node;

            internal void Insert(Expression expr)
            {
                if(nodeOrList.First.TryGet(out var list))
                    nodeOrList = list.AddLast(expr);
                else if(nodeOrList.Second.TryGet(out var node))
                    nodeOrList = list.AddAfter(node, expr);
            }
        }

        private sealed class RewritePoint: LinkedList<Expression>
        {
            internal CodeInsertionPoint CaptureInsertionPoint() => First is null ? new CodeInsertionPoint(this) : new CodeInsertionPoint(Last);

            internal Expression Rewrite(Expression expression)
                =>  First is null ? expression : Expression.Block(typeof(void), this.Concat(Sequence.Single(expression)));
        }
        private static readonly UserDataSlot<RewritePoint> StatementSlot = UserDataSlot<RewritePoint>.Allocate ();
        private static readonly UserDataSlot<bool> ContainsAwaitSlot = UserDataSlot<bool>.Allocate ();

        internal static void MarkAsRewritePoint (Expression node) => node.GetUserData ().Set (StatementSlot, new RewritePoint ());

        internal new void Push (Expression node)
        {
            var current = Count == 0 ? null : Peek ();
            //block cannot be treated as statement
            if (!(node is BlockExpression) && (current is null || current is BlockExpression || current is TryExpression))
                MarkAsRewritePoint (node);
            base.Push (node);
        }

        internal static bool ContainsAwait (Expression expr) => expr.GetUserData ().Get (ContainsAwaitSlot, false);

        internal void ContainsAwait ()
        {
            foreach (var lookup in this)
                if (lookup is BlockExpression)
                    return;
                else
                    lookup.GetUserData ().Set (ContainsAwaitSlot, true);
        }

        internal Expression Pop (Expression node)
        {
            var current = Pop ();
            return current.GetUserData ().Remove (StatementSlot, out var replacement) ? replacement.Rewrite (node) : node;
        }

        internal Metaprogramming.CodeInsertionPoint GetCodeInsertionPoint()
        {
            foreach (var lookup in this)
            {
                var statement = lookup.GetUserData ().Get (StatementSlot);
                if (!(statement is null))
                    return statement.CaptureInsertionPoint().Insert;
            }
            //should never be happened
            //this exception indicates that caller code tries to get code insertion point on empty expression tree
            throw new InvalidOperationException();
        }

        internal Expression Rewrite<E>(E expression, Converter<E, Expression> rewriter)
            where E : Expression
        {
            Push (expression);
            var result = rewriter (expression);
            return Pop(result);
        }
    }
}