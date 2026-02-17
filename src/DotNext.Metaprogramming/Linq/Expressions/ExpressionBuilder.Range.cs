namespace DotNext.Linq.Expressions;

partial class ExpressionBuilder
{
    /// <summary>
    /// Extends <see cref="System.Index"/> type.
    /// </summary>
    /// <param name="index">The index value.</param>
    extension(in Index index)
    {
        /// <summary>
        /// Converts index to equivalent expression.
        /// </summary>
        /// <value>Index expression.</value>
        public ItemIndexExpression Quoted => Index(index.Value, index.IsFromEnd);
    }

    /// <summary>
    /// Extends <see cref="Range"/> type.
    /// </summary>
    /// <param name="range">The range value.</param>
    extension(in Range range)
    {
        /// <summary>
        /// Converts range to equivalent expression.
        /// </summary>
        /// <value>The expression representing given range.</value>
        public RangeExpression Quoted => range.Start.Quoted.To(range.End.Quoted);
    }
}