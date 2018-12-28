namespace Cheats.Generics
{
    /// <summary>
    /// Represents boolean constant as generic parameter.
    /// </summary>
    public abstract class Bool: Constant<bool>
    {
        private Bool(bool value)
            : base(value)
        {
        }

        /// <summary>
        /// Represents TRUE constant value as generic parameter.
        /// </summary>
        public sealed class True: Bool
        {
            public True()
                : base(true)
            {
            }
        }

        /// <summary>
        /// Represents FALSE constant value as generic parameter.
        /// </summary>
        public sealed class False: Bool
        {
            public False()
                : base(false)
            {
            }
        }

        /// <summary>
        /// Extracts boolean constant from generic parameter.
        /// </summary>
        /// <typeparam name="G">Type of boolean generic.</typeparam>
        /// <returns>Boolean value.</returns>
        public static new bool Of<G>()
            where G: Bool, new()
            => Constant<bool>.Of<G>();
    }
}