using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    /// <summary>
    /// Converts block expressions located at the right side of expressions
    /// with statements.
    /// </summary>
    /// <remarks>
    /// Block expression:
    /// x = { a; b;}
    /// will be replaced with
    /// a;
    /// x = b;
    /// Conditional expression:
    /// x = a ? b : c;
    /// into
    /// var temp;
    /// if(a)
    ///   temp = b;
    /// else
    ///   temp = c;
    /// 
    /// This transformation is required for state machine method body
    /// because we can't jump inside of block expressions using labels.
    /// </remarks>
    internal sealed class BlockSimplifier: ExpressionVisitor
    {
        private class ExpressionLookup
        {
            internal readonly Expression Expr;
            internal readonly ExpressionLookup Parent;

            internal ExpressionLookup(Expression expr, ExpressionLookup parent)
            {
                Expr = expr;
                Parent = parent;
            }

            internal bool IsRoot => Parent is null;

            internal virtual Expression Rewrite(Expression expression) => expression;
        }

        private sealed class StatementLookup : ExpressionLookup
        {
            private readonly ICollection<Expression> expressions;
            private readonly ICollection<ParameterExpression> variables;

            internal StatementLookup(Expression expr, ExpressionLookup parent)
                : base(expr, parent)
            {
                expressions = new LinkedList<Expression>();
                variables = new LinkedList<ParameterExpression>();
            }

            internal Expression Relocate(BlockExpression block)
            {
                block.Variables.ForEach(variables.Add);
                //move all expressions from block to statement level except last one
                for (var i = 0; i < block.Expressions.Count - 1; i++)
                    expressions.Add(block.Expressions[i]);
                return block.Result;
            }

            internal override Expression Rewrite(Expression expression)
                => variables.Count == 0 && expressions.Count == 0 ? expression : Expression.Block(variables, expressions.Concat(Sequence.Single(expression)));
        }
        //lexical scope stack
        private ExpressionLookup current;

        private BlockSimplifier()
        {
        }

        private void Push(Expression expr)
            => current = (current?.Expr is BlockExpression || current?.Expr is ConditionalExpression) && expr.Type == typeof(void) ?
                    new StatementLookup(expr, current) :
                    new ExpressionLookup(expr, current);

        private void Pop() => current = current.Parent;

        private Expression Visit<E>(E expression, Func<E, Expression> visitor)
        {

        }

        public override Expression Visit(Expression node)
        {
            Push(node);
            try
            {
                return current.Rewrite(base.Visit(node));
            }
            finally
            {
                Pop();
            }
        }

        private StatementLookup FindStatement()
        {
            var current = this.current?.Parent;
            while (!(current is null || current is StatementLookup))
                current = current.Parent;
            return current as StatementLookup;
        }

        //move block instructions to the statement level except last expression
        protected override Expression VisitBlock(BlockExpression node)
        {
            StatementLookup statement;
            return current.IsRoot || (statement = FindStatement()) is null || ReferenceEquals(node, statement.Expr) ?
                base.VisitBlock(node) :
                Visit(statement.Relocate(node));
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if(node.IsSimpleExpression())
            {

                //declare new variable in the statement
            }
        }

        internal static Expression Simplify(BlockExpression block)
        {
            var simplifier = new BlockSimplifier();
            return simplifier.Visit(block);
        }
    }
}
