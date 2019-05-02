using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Provides an expression refer to a single object or structure so
    /// that body can use a simplified syntax when accessing member of the object
    /// or structure.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement">With..End Statement</seealso>
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

        public static WithExpression Create(Expression obj, Statement body)
        {
            var result = new WithExpression(obj);
            result.Body = body(result.Variable);
            return result;
        }

        public static WithExpression Create(Expression obj, Expression body)
            => new WithExpression(obj) { Body = body };

        public new ParameterExpression Variable { get; }

        /// <summary>
        /// Gets body of the statement.
        /// </summary>
        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        /// <summary>
        /// Always returns <see langword="true"/> because
        /// this expression is <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Always returns <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Gets type of this expression.
        /// </summary>
        public override Type Type => Body.Type;

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
            => assignment is null ? Body : Block(Sequence.Singleton(Variable), assignment, Body);
    }
}
