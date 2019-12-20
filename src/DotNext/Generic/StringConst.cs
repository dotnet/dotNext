namespace DotNext.Generic
{
    /// <summary>
    /// Represents string constant as generic parameter.
    /// </summary>
    public abstract class StringConst : Constant<string>
    {
        /// <summary>
        /// Initializes string constant.
        /// </summary>
        /// <param name="value">Constant value.</param>
        protected StringConst(string value)
            : base(value)
        {
        }

        /// <summary>
        /// Represents <see langword="null"/> as string constant.
        /// </summary>
        public sealed class Null : StringConst
        {
            /// <summary>
            /// Represents constant value.
            /// </summary>
            public const string Value = null;

            /// <summary>
            /// Initializes a new <see langword="null"/> string constant.
            /// </summary>
            public Null()
                : base(Value)
            {
            }
        }

        /// <summary>
        /// Represents empty string constant.
        /// </summary>
        public sealed class Empty : StringConst
        {
            /// <summary>
            /// Represents constant value.
            /// </summary>
            public const string Value = "";

            /// <summary>
            /// Creates empty string constant.
            /// </summary>
            public Empty()
                : base(Value)
            {
            }
        }
    }
}
