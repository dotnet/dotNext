using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    using VariantType;

    public sealed class WithExpression : Expression
    {
        public delegate Expression Statement(ParameterExpression scopeVar);

        private readonly BinaryExpression assignment;
        private Expression body;

        internal WithExpression(Expression expr)
        {
            if (expr is ParameterExpression variable)
                Variable = variable;
            else
            {
                Variable = Expression.Variable(expr.Type, "scopeVar");
                assignment = Assign(Variable, expr);
            }
        }

        public WithExpression(Expression expr, Expression body)
            : this(expr)
        {
            this.body = body;
        }

        public WithExpression(Expression expr, Statement body)
            : this(expr)
        {
            this.body = body(Variable);
        }

        public new ParameterExpression Variable { get; }

        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        public override bool CanReduce => true;

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => Body.Type;

        public override Expression Reduce()
            => assignment is null ? Body : Block(Sequence.Singleton(Variable), assignment, Body);
    }
}
