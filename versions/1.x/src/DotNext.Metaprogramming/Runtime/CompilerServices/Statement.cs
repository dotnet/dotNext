using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using VariantType;
    using static Collections.Generic.Collection;
    using static Linq.Expressions.ExpressionBuilder;

    /// <summary>
    /// Represents statement.
    /// </summary>
    internal class Statement : Expression
    {
        private sealed class CodeInsertionPoint
        {
            private Variant<LinkedList<Expression>, LinkedListNode<Expression>> nodeOrList;

            internal CodeInsertionPoint(LinkedList<Expression> list) => nodeOrList = list;
            internal CodeInsertionPoint(LinkedListNode<Expression> node) => nodeOrList = node;

            internal void Insert(Expression expr)
            {
                if (nodeOrList.First.TryGet(out var list))
                    nodeOrList = list.AddLast(expr);
                else if (nodeOrList.Second.TryGet(out var node))
                    nodeOrList = node.List.AddAfter(node, expr);
            }
        }

        private protected readonly LinkedList<Expression> prologue;
        private protected readonly LinkedList<Expression> epilogue;
        internal readonly Expression Content;

        internal Statement(Expression expression)
            : this(expression, Enumerable.Empty<Expression>(), Enumerable.Empty<Expression>())
        {
        }

        private Statement(Expression expression, IEnumerable<Expression> prologue, IEnumerable<Expression> epilogue)
        {
            Content = expression ?? Empty();
            if (expression is Statement stmt)
            {
                InsertIntoHead(prologue, this.prologue = stmt.prologue);
                InsertIntoHead(epilogue, this.epilogue = stmt.epilogue);
            }
            else
            {
                this.prologue = new LinkedList<Expression>(prologue);
                this.epilogue = new LinkedList<Expression>(epilogue);
            }
        }

        private static void InsertIntoHead(IEnumerable<Expression> source, LinkedList<Expression> destination)
        {
            if (destination.First is null)
                destination.AddAll(source);
            else
            {
                var first = destination.First;
                foreach (var expr in source)
                    destination.AddBefore(first, expr);
            }
        }

        internal static Expression Wrap(Expression expr)
        {
            switch (expr)
            {
                case TryExpression seh:
                    return seh;
                case BlockExpression block:
                    Rewrite(ref block);
                    return block;
                case LoopExpression loop:
                    Rewrite(ref loop);
                    return loop;
                case SwitchExpression sw:
                    Rewrite(ref sw);
                    return sw;
                case Statement stmt:
                    return stmt;
                default:
                    return new Statement(expr);
            }
        }

        internal static void Rewrite(ref LoopExpression loop)
            => loop = loop.Update(loop.BreakLabel, loop.ContinueLabel, Wrap(loop.Body));

        internal static void Rewrite(ref BlockExpression block)
            => block = block.Update(block.Variables, block.Expressions.Select(Wrap));

        internal static void Rewrite(ref SwitchExpression @switch)
            => @switch = @switch.Update(@switch.SwitchValue, @switch.Cases.Select(c => c.Update(c.TestValues, Wrap(c.Body))), Wrap(@switch.DefaultBody));

        private static CodeInsertionPoint CaptureRewritePoint(LinkedList<Expression> codeBlock)
            => codeBlock.First is null ? new CodeInsertionPoint(codeBlock) : new CodeInsertionPoint(codeBlock.Last);

        internal DotNext.CodeInsertionPoint PrologueCodeInserter() => CaptureRewritePoint(prologue).Insert;

        internal DotNext.CodeInsertionPoint EpilogueCodeInserter() => CaptureRewritePoint(epilogue).Insert;

        public sealed override Type Type => Content.Type;
        public sealed override ExpressionType NodeType => ExpressionType.Extension;
        public sealed override Expression Reduce() =>
            Content.AddPrologue(false, prologue).AddEpilogue(false, epilogue);

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            if (Content is Statement stmt)
                return stmt.VisitChildren(visitor);

            var expression = visitor.Visit(Content);
            return ReferenceEquals(expression, Content) ? this : new Statement(expression, prologue, epilogue);
        }

        public override bool CanReduce => true;
    }
}
