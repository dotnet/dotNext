using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    using Seq = Collections.Generic.Sequence;

    /// <summary>
    /// Provides an expression refer to a single object or structure so
    /// that body can use a simplified syntax when accessing member of the object
    /// or structure.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement">With..End Statement</seealso>
    public sealed class WithExpression : CustomExpression
    {
        /// <summary>
        /// Represents constructor of the expression body.
        /// </summary>
        /// <param name="scopeVar">The variable representing referred object or structure.</param>
        /// <returns>The body of the expression.</returns>
        public delegate Expression Statement(ParameterExpression scopeVar);

        private readonly BinaryExpression? assignment;
        private Expression? body;

        internal WithExpression(Expression expr)
        {
            if (expr is ParameterExpression variable)
            {
                Variable = variable;
            }
            else
            {
                Variable = Expression.Variable(expr.Type, "scopeVar");
                assignment = Assign(Variable, expr);
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="WithExpression"/>.
        /// </summary>
        /// <param name="obj">The object to be referred inside of the body.</param>
        /// <param name="body">The body of the expression.</param>
        /// <returns>The constructed expression.</returns>
        public static WithExpression Create(Expression obj, Statement body)
        {
            var result = new WithExpression(obj);
            result.Body = body(result.Variable);
            return result;
        }

        /// <summary>
        /// Creates a new instance of <see cref="WithExpression"/>.
        /// </summary>
        /// <param name="obj">The object to be referred inside of the body.</param>
        /// <param name="body">The body of the expression.</param>
        /// <returns>The constructed expression.</returns>
        public static WithExpression Create(Expression obj, Expression body)
            => new(obj) { Body = body };

        /// <summary>
        /// The expression representing referred object inside of <see cref="Body"/>.
        /// </summary>
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
        /// Gets type of this expression.
        /// </summary>
        public override Type Type => Body.Type;

        /// <summary>
        /// Reconstructs <see cref="WithExpression"/> with a new body.
        /// </summary>
        /// <param name="body">A new body to be placed into this expression.</param>
        /// <returns>The expression updated with the given body.</returns>
        public WithExpression Update(Expression body) => new(assignment is null ? Variable : assignment.Right) { Body = body };

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
            => assignment is null ? Body : Block(Seq.Singleton(Variable), assignment, Body);
    }
}
