using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Linq.Expressions;

using CharBufferAllocator = Buffers.MemoryAllocator<char>;
using InterpolatedStringBuilder = Runtime.CompilerServices.InterpolatedStringBuilder;

public partial class InterpolationExpression
{
    /// <summary>
    /// Represents interpolated string as an expression.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct InterpolatedStringExpressionHandler
    {
        private Expression[]? arguments;
        private int index;
        private InterpolatedStringBuilder builder;

        /// <summary>
        /// Initializes a new handler.
        /// </summary>
        /// <param name="literalLength">The total number of characters in known at compile-time.</param>
        /// <param name="formattedCount">The number of placeholders.</param>
        public InterpolatedStringExpressionHandler(int literalLength, int formattedCount)
        {
            builder = new(literalLength, formattedCount);
            arguments = new Expression[formattedCount];
            index = 0;
        }

        /// <summary>
        /// Adds literal value to the template.
        /// </summary>
        /// <param name="literal">The literal part of the template.</param>
        public readonly void AppendLiteral(string? literal) => builder.AppendLiteral(literal);

        /// <summary>
        /// Adds a placeholder.
        /// </summary>
        /// <param name="arg">The expression representing the argument of the interpolated string.</param>
        /// <param name="alignment">The alignment of the argument withing the string.</param>
        /// <param name="format">The format of the argument.</param>
        /// <exception cref="ArgumentNullException"><paramref name="arg"/> is <see langword="null"/>.</exception>
        public void AppendFormatted(Expression arg, int alignment, string? format = null)
        {
            if (arg is null)
                throw new ArgumentNullException(nameof(arg));

            builder.AppendFormatted(arg.Type, alignment, format);
            Arguments[index++] = arg;
        }

        /// <summary>
        /// Adds a placeholder.
        /// </summary>
        /// <param name="arg">The expression representing the argument of the interpolated string.</param>
        /// <param name="format">The format of the argument.</param>
        /// <exception cref="ArgumentNullException"><paramref name="arg"/> is <see langword="null"/>.</exception>
        public void AppendFormatted(Expression arg, string? format = null)
            => AppendFormatted(arg, 0, format);

        internal readonly Expression[] Arguments => arguments ?? Array.Empty<Expression>();

        internal readonly LambdaExpression BuildRenderer() => builder.Build();

        /// <summary>
        /// Gets original template.
        /// </summary>
        /// <returns>The original template.</returns>
        public override string ToString() => builder.ToString();
    }

    private InterpolationExpression(ref InterpolatedStringExpressionHandler handler, Expression? formatProvider)
        : this(handler.ToString(), handler.Arguments, Kind.InterpolatedString, formatProvider)
    {
        interpolation = handler.BuildRenderer();
    }

    private InvocationExpression MakeInterpolatedString()
    {
        Debug.Assert(interpolation is not null);
        return Expression.Invoke(interpolation, arguments.Prepend(FormatProvider).Append(Expression.Constant(null, typeof(CharBufferAllocator))));
    }

    /// <summary>
    /// Creates an expression representing the interpolated string.
    /// </summary>
    /// <param name="handler">The interpolated string.</param>
    /// <param name="formatProvider">The expression of type <see cref="IFormatProvider"/>.</param>
    /// <returns>The expression representing the interpolated string.</returns>
    public static InterpolationExpression Create(ref InterpolatedStringExpressionHandler handler, Expression? formatProvider = null)
        => new(ref handler, formatProvider);
}