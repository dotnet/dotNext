namespace DotNext.Generic
{
    /// <summary>
    /// Represents default value of type <typeparamref name="T"/> as constant.
    /// </summary>
    /// <typeparam name="T">The type of the constant value.</typeparam>
    public sealed class DefaultConst<T> : Constant<T>
    {
        /// <summary>
        /// Initializes a new constant equal to default value of type <typeparamref name="T"/>.
        /// </summary>
        public DefaultConst()
            : base(default)
        {
        }
    }
}
