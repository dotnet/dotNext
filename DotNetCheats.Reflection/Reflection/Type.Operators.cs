namespace Cheats.Reflection
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
            /// <returns>Unary operator.</returns>
            public static UnaryOperator<T, R> Get<R>(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Operators<R>.GetOrCreate(op, lookup);
            public static UnaryOperator<T, R> Require<R>(UnaryOperator op, OperatorLookup lookup = OperatorLookup.Any) => Get<R>(op, lookup) ?? throw MissingOperatorException.Create<T>(op);
        }
    }
}