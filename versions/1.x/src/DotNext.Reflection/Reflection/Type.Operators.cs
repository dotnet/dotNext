namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Represents unary operator applicable to type <typeparamref name="T"/>.
        /// </summary>
        public static class Operator
        {
            /// <summary>
            /// Gets unary operator. 
            /// </summary>
            /// <param name="op">Unary operator type.</param>
            /// <param name="lookup">Operator resolution strategy.</param>
            /// <typeparam name="R">Result of unary operator.</typeparam>
            /// <returns>Unary operator; or <see langword="null"/>, if it doesn't exist.</returns>
            public static UnaryOperator<T, R> Get<R>(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => UnaryOperator<T, R>.GetOrCreate(op, lookup);

            /// <summary>
            /// Gets unary operator of the same result type as its operand.
            /// </summary>
            /// <param name="op">Unary operator type.</param>
            /// <param name="lookup">Operator resolution strategy.</param>
            /// <returns>Unary operator; or <see langword="null"/>, if it doesn't exist.</returns>
            public static UnaryOperator<T, T> Get(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Get<T>(op, lookup);

            /// <summary>
            /// Gets unary operator. 
            /// </summary>
            /// <param name="op">Unary operator type.</param>
            /// <param name="lookup">Operator resolution strategy.</param>
            /// <typeparam name="R">Result of unary operator.</typeparam>
            /// <returns>Unary operator.</returns>
            /// <exception cref="MissingOperatorException">Operator doesn't exist.</exception>
            public static UnaryOperator<T, R> Require<R>(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Get<R>(op, lookup) ?? throw MissingOperatorException.Create<T>(op);

            /// <summary>
            /// Gets unary operator of the same result type as its operand.
            /// </summary>
            /// <param name="op">Unary operator type.</param>
            /// <param name="lookup">Operator resolution strategy.</param>
            /// <returns>Unary operator.</returns>
            /// <exception cref="MissingOperatorException">Operator doesn't exist.</exception>
            public static UnaryOperator<T, T> Require(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Require<T>(op, lookup);
        }

        /// <summary>
        /// Represents binary operator applicable to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="U">Type of second operand.</typeparam>
        public static class Operator<U>
        {
            /// <summary>
            /// Gets binary operator. 
            /// </summary>
            /// <param name="op">Binary operator type.</param>
            /// <param name="lookup">Operator resolution strategy.</param>
            /// <typeparam name="R">Result of binary operator.</typeparam>
            /// <returns>Binary operator; or <see langword="null"/>, if it doesn't exist.</returns>
            public static BinaryOperator<T, U, R> Get<R>(BinaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => BinaryOperator<T, U, R>.GetOrCreate(op, lookup);

            /// <summary>
            /// Gets binary operator. 
            /// </summary>
            /// <param name="op">Binary operator type.</param>
            /// <param name="lookup">Operator resolution strategy.</param>
            /// <typeparam name="R">Result of binary operator.</typeparam>
            /// <returns>Binary operator.</returns>
            /// <exception cref="MissingOperatorException">Operator doesn't exist.</exception>
            public static BinaryOperator<T, U, R> Require<R>(BinaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Get<R>(op, lookup) ?? throw MissingOperatorException.Create<T>(op);
        }
    }
}