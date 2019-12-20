using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions
{
    using static Reflection.DisposableType;

    /// <summary>
    /// Represents <c>using</c> expression.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">USING statement</seealso>
    public sealed class UsingExpression : Expression
    {
        /// <summary>
        /// Represents constructor of <c>using</c> expression.
        /// </summary>
        /// <param name="resource">The variable representing disposable resource.</param>
        /// <returns>Body of <c>using</c> expression.</returns>
        public delegate Expression Statement(ParameterExpression resource);

        private readonly MethodInfo disposeMethod;
        private readonly BinaryExpression assignment;
        private Expression body;

        internal UsingExpression(Expression resource)
        {
            disposeMethod = resource.Type.GetDisposeMethod() ?? throw new ArgumentNullException(ExceptionMessages.DisposePatternExpected(resource.Type));
            if (resource is ParameterExpression param)
            {
                assignment = null;
                Resource = param;
            }
            else
                assignment = Assign(Resource = Variable(resource.Type, "resource"), resource);
        }

        /// <summary>
        /// Creates block of code associated with disposable resource.
        /// </summary>
        /// <param name="resource">The disposable resource.</param>
        /// <param name="body">The delegate used to construct the block of code.</param>
        /// <returns>The constructed expression.</returns>
        public static UsingExpression Create(Expression resource, Statement body)
        {
            var result = new UsingExpression(resource);
            result.Body = body(result.Resource);
            return result;
        }

        /// <summary>
        /// Creates block of code associated with disposable resource.
        /// </summary>
        /// <param name="resource">The disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <returns>The constructed expression.</returns>
        public static UsingExpression Create(Expression resource, Expression body)
            => new UsingExpression(resource) { Body = body };

        /// <summary>
        /// Gets body of <c>using</c> expression.
        /// </summary>
        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        /// <summary>
        /// Gets the variable holding the disposable resource.
        /// </summary>
        public ParameterExpression Resource { get; }

        /// <summary>
        /// Always returns <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Gets the type of this expression.
        /// </summary>
        public override Type Type => Body.Type;

        /// <summary>
        /// Always returns <see langword="true"/> because
        /// this expression is <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Reconstructs expression with a new body.
        /// </summary>
        /// <param name="body">The new body of this expression.</param>
        /// <returns>Updated expression.</returns>
        public UsingExpression Update(Expression body)
        {
            var result = assignment is null ? new UsingExpression(Resource) : new UsingExpression(assignment.Right);
            result.Body = body;
            return result;
        }

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            if (assignment is null)
                return TryFinally(Body, Block(typeof(void), Call(Resource, disposeMethod), Assign(Resource, Default(Resource.Type))));
            else
                return Block(Sequence.Singleton(Resource), assignment, TryFinally(Body, Call(Resource, disposeMethod)));
        }
    }
}