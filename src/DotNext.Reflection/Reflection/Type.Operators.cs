namespace DotNext.Reflection
{
    public static partial class Type<T>
    {

        /// <summary>
        /// Represents unary operator applicable to type <typeparamref name="T"/>.
        /// </summary>
        public static class Operator
        {
            private sealed class Operators<R> : Cache<Reflection.Operator.Kind, UnaryOperator<T, R>>
            {
                private static readonly Cache<Reflection.Operator.Kind, UnaryOperator<T, R>> Instance = new Operators<R>();
                private Operators()
                {
                }

                private protected override UnaryOperator<T, R> Create(Reflection.Operator.Kind @operator) => UnaryOperator<T, R>.Reflect(@operator);

                internal static UnaryOperator<T, R> GetOrCreate(UnaryOperator @operator, OperatorLookup lookup)
                {
                    switch(lookup)
                    {
                        case OperatorLookup.Predefined:
                            return Instance.GetOrCreate(new Reflection.Operator.Kind(@operator, false));
                        case OperatorLookup.Overloaded:
                            return Instance.GetOrCreate(new Reflection.Operator.Kind(@operator, true));
                        case OperatorLookup.Any:
                            return Instance.GetOrCreate(new Reflection.Operator.Kind(@operator, true)) ??
                                Instance.GetOrCreate(new Reflection.Operator.Kind(@operator, false));
                        default:
                            return null;
                    }
                }
            }

            /// <summary>
            /// Gets unary operator. 
            /// </summary>
            /// <param name="op">Unary operator type.</param>
            /// <typeparam name="R">Result of unary operator.</typeparam>
            /// <returns>Unary operator; or null, if it doesn't exist.</returns>
            public static UnaryOperator<T, R> Get<R>(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Operators<R>.GetOrCreate(op, lookup);

            /// <summary>
            /// Gets unary operator of the same result type as its operand.
            /// </summary>
            /// <param name="op">Unary operator type.</param>
            /// <returns>Unary operator; or null, if it doesn't exist.</returns>
            public static UnaryOperator<T, T> Get(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Get<T>(op, lookup);

            /// <summary>
            /// Gets unary operator. 
            /// </summary>
            /// <param name="op">Unary operator type.</param>
            /// <typeparam name="R">Result of unary operator.</typeparam>
            /// <returns>Unary operator.</returns>
            /// <exception cref="MissingOperatorException">Operator doesn't exist.</exception>
            public static UnaryOperator<T, R> Require<R>(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Get<R>(op, lookup) ?? throw MissingOperatorException.Create<T>(op);

            /// <summary>
            /// Gets unary operator of the same result type as its operand.
            /// </summary>
            /// <param name="op">Unary operator type.</param>
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
            private sealed class Operators<R> : Cache<Reflection.Operator.Kind, BinaryOperator<T, U, R>>
            {
                private static readonly Cache<Reflection.Operator.Kind, BinaryOperator<T, U, R>> Instance = new Operators<R>();
                private Operators()
                {
                }

                private protected override BinaryOperator<T, U, R> Create(Reflection.Operator.Kind @operator) => BinaryOperator<T, U, R>.Reflect(@operator);

                internal static BinaryOperator<T, U, R> GetOrCreate(BinaryOperator @operator, OperatorLookup lookup)
                {
                    switch(lookup)
                    {
                        case OperatorLookup.Predefined:
                            return Instance.GetOrCreate(new Reflection.Operator.Kind(@operator, false));
                        case OperatorLookup.Overloaded:
                            return Instance.GetOrCreate(new Reflection.Operator.Kind(@operator, true));
                        case OperatorLookup.Any:
                            return Instance.GetOrCreate(new Reflection.Operator.Kind(@operator, true)) ??
                                Instance.GetOrCreate(new Reflection.Operator.Kind(@operator, false));
                        default:
                            return null;
                    }
                }
            }

            /// <summary>
            /// Gets binary operator. 
            /// </summary>
            /// <param name="op">Binary operator type.</param>
            /// <typeparam name="R">Result of binary operator.</typeparam>
            /// <returns>Binary operator; or null, if it doesn't exist.</returns>
            public static BinaryOperator<T, U, R> Get<R>(BinaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Operators<R>.GetOrCreate(op, lookup);

            /// <summary>
            /// Gets binary operator. 
            /// </summary>
            /// <param name="op">Binary operator type.</param>
            /// <typeparam name="R">Result of binary operator.</typeparam>
            /// <returns>Binary operator.</returns>
            /// <exception cref="MissingOperatorException">Operator doesn't exist.</exception>
            public static BinaryOperator<T, U, R> Require<R>(BinaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Get<R>(op, lookup) ?? throw MissingOperatorException.Create<T>(op);
        }
    }
}