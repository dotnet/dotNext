namespace DotNetCheats.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Represents unary operator applicable to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="R">Type of unary operator result.</typeparam>
        public static class UnaryOperator<R>
        {
            private sealed class Operators : Cache<UnaryOperator, UnaryOperator<T, R>>
            {
                private static readonly Cache<UnaryOperator, UnaryOperator<T, R>> Instance = new Operators();
                private Operators()
                {
                }

                private protected override UnaryOperator<T, R> Create(UnaryOperator @operator) => UnaryOperator<T, R>.Reflect(@operator);

                internal static new UnaryOperator<T, R> GetOrCreate(UnaryOperator @operator) => Instance.GetOrCreate(@operator);
            }

            /// <summary>
            /// Gets unary operator. 
            /// </summary>
            /// <param name="op">Unary operator type.</param>
            /// <returns>Unary operator.</returns>
            public static UnaryOperator<T, R> Get(UnaryOperator op) => Operators.GetOrCreate(op);

            public static UnaryOperator<T, R> Require(UnaryOperator op) => Get(op) ?? throw MissingOperatorException.Create<T>(op);
        }
    }
}