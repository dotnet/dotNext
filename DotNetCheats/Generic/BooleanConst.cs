namespace Cheats.Generic
{
    /// <summary>
    /// Represents boolean constant as generic parameter.
    /// </summary>
    public abstract class BooleanConst: Constant<bool>
    {
        private BooleanConst(bool value)
            : base(value)
        {
        }

        /// <summary>
        /// Represents TRUE constant value as generic parameter.
        /// </summary>
        public sealed class True: BooleanConst
        {
            public True()
                : base(true)
            {
            }
        }

        /// <summary>
        /// Represents FALSE constant value as generic parameter.
        /// </summary>
        public sealed class False: BooleanConst
        {
            public False()
                : base(false)
            {
            }
        }
    }
}