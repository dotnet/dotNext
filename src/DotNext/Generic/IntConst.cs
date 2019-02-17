namespace DotNext.Generic
{
    /// <summary>
    /// Represents integer constant as type.
    /// </summary>
    public abstract class IntConst: Constant<int>
    {
        /// <summary>
        /// Associated integer value with this type.
        /// </summary>
        /// <param name="value">A value to be associated with this type.</param>
        protected IntConst(int value)
            : base(value)
        {
        }

        /// <summary>
        /// Represents zero value as type.
        /// </summary>
        public sealed class Zero: IntConst
        {
            /// <summary>
            /// Initializes a new constant value.
            /// </summary>
            public Zero()
                : base(0)
            {
            }
        }

        /// <summary>
        /// Represents max integer value as type.
        /// </summary>
        public sealed class Max: IntConst
        {
            public Max()
                : base(int.MaxValue)
            {
            }
        }

        /// <summary>
        /// Represents min integer value as type.
        /// </summary>
        public sealed class Min: IntConst
        {
            public Min()
                : base(int.MinValue)
            {
            }
        }
    }
}