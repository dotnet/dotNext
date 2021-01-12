namespace DotNext.Generic
{
    /// <summary>
    /// Represents <see cref="int"/> constant as type.
    /// </summary>
    public abstract class IntConst : Constant<int>
    {
        /// <summary>
        /// Associated <see cref="int"/> value with this type.
        /// </summary>
        /// <param name="value">A value to be associated with this type.</param>
        protected IntConst(int value)
            : base(value)
        {
        }

        /// <summary>
        /// Represents zero value as type.
        /// </summary>
        public sealed class Zero : IntConst
        {
            /// <summary>
            /// Represents constant value.
            /// </summary>
            public const int Value = 0;

            /// <summary>
            /// Initializes a new constant value.
            /// </summary>
            public Zero()
                : base(Value)
            {
            }
        }

        /// <summary>
        /// Represents max integer value as type.
        /// </summary>
        public sealed class Max : IntConst
        {
            /// <summary>
            /// Represents constant value.
            /// </summary>
            public const int Value = int.MaxValue;

            /// <summary>
            /// Initializes a new constant value.
            /// </summary>
            public Max()
                : base(Value)
            {
            }
        }

        /// <summary>
        /// Represents min integer value as type.
        /// </summary>
        public sealed class Min : IntConst
        {
            /// <summary>
            /// Represents constant value.
            /// </summary>
            public const int Value = int.MinValue;

            /// <summary>
            /// Initializes a new constant value.
            /// </summary>
            public Min()
                : base(Value)
            {
            }
        }
    }
}