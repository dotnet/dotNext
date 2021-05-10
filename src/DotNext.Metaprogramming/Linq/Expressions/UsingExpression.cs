using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace DotNext.Linq.Expressions
{
    using static Reflection.DisposableType;
    using Seq = Collections.Generic.Sequence;

    /// <summary>
    /// Represents <c>using</c> or <c>await using</c> expression.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement">USING statement</seealso>
    public sealed class UsingExpression : CustomExpression
    {
        /// <summary>
        /// Represents constructor of <c>using</c> expression.
        /// </summary>
        /// <param name="resource">The variable representing disposable resource.</param>
        /// <returns>Body of <c>using</c> expression.</returns>
        public delegate Expression Statement(ParameterExpression resource);

        private readonly MethodInfo disposeMethod;
        private readonly BinaryExpression? assignment;
        private readonly bool? configureAwait;  // null for synchronous expression
        private Expression? body;

        internal UsingExpression(Expression resource)
        {
            disposeMethod = resource.Type.GetDisposeMethod() ?? throw new ArgumentException(ExceptionMessages.DisposePatternExpected(resource.Type), nameof(resource));
            if (resource is ParameterExpression param)
            {
                assignment = null;
                Resource = param;
            }
            else
            {
                assignment = Assign(Resource = Variable(resource.Type, "resource"), resource);
            }
        }

        internal UsingExpression(Expression resource, bool configureAwait)
        {
            disposeMethod = resource.Type.GetDisposeAsyncMethod() ?? throw new ArgumentException(ExceptionMessages.DisposePatternExpected(resource.Type), nameof(resource));
            if (resource is ParameterExpression param)
            {
                assignment = null;
                Resource = param;
            }
            else
            {
                assignment = Assign(Resource = Variable(resource.Type, "resource"), resource);
            }

            this.configureAwait = configureAwait;
        }

        /// <summary>
        /// Creates a block of code associated with disposable resource.
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
        /// Creates a block of code associated with asynchronously disposable resource.
        /// </summary>
        /// <param name="resource">The disposable resource.</param>
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="ValueTask.ConfigureAwait(bool)"/> with <see langword="false"/> argument when awaiting <see cref="IAsyncDisposable.DisposeAsync"/> method.</param>
        /// <param name="body">The delegate used to construct the block of code.</param>
        /// <returns>The constructed expression.</returns>
        /// <seealso cref="IsAwaitable"/>
        public static UsingExpression Create(Expression resource, bool configureAwait, Statement body)
        {
            var result = new UsingExpression(resource, configureAwait);
            result.Body = body(result.Resource);
            return result;
        }

        /// <summary>
        /// Creates a block of code associated with disposable resource.
        /// </summary>
        /// <param name="resource">The disposable resource.</param>
        /// <param name="body">The body of the statement.</param>
        /// <returns>The constructed expression.</returns>
        public static UsingExpression Create(Expression resource, Expression body)
            => new(resource) { Body = body };

        /// <summary>
        /// Creates a block of code associated with asynchronously disposable resource.
        /// </summary>
        /// <param name="resource">The disposable resource.</param>
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="ValueTask.ConfigureAwait(bool)"/> with <see langword="false"/> argument when awaiting <see cref="IAsyncDisposable.DisposeAsync"/> method.</param>
        /// <param name="body">The body of the statement.</param>
        /// <returns>The constructed expression.</returns>
        /// <seealso cref="IsAwaitable"/>
        public static UsingExpression Create(Expression resource, bool configureAwait, Expression body)
            => new(resource, configureAwait) { Body = body };

        /// <summary>
        /// Indicates that this <c>using</c> block is asynchronous.
        /// </summary>
        public bool IsAwaitable => configureAwait.HasValue;

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
        /// Gets the type of this expression.
        /// </summary>
        public override Type Type => configureAwait is null ? Body.Type : typeof(void);

        /// <summary>
        /// Reconstructs expression with a new body.
        /// </summary>
        /// <param name="body">The new body of this expression.</param>
        /// <returns>Updated expression.</returns>
        public UsingExpression Update(Expression body)
        {
            var resource = assignment is null ? Resource : assignment.Right;
            var result = this.configureAwait.TryGetValue(out var configureAwait) ?
                new UsingExpression(resource, configureAwait) :
                new UsingExpression(resource);
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
            Expression disposeCall = Call(Resource, disposeMethod);
            if (this.configureAwait.TryGetValue(out var configureAwait))
            {
                disposeCall = disposeCall.Await(configureAwait);
            }

            return assignment is null ?
                MakeTry(Type, Body, Block(typeof(void), disposeCall, Assign(Resource, Default(Resource.Type))), null, null) :
                Block(Type, Seq.Singleton(Resource), assignment, TryFinally(Body, disposeCall));
        }
    }
}