namespace DotNext.Generic;

/// <summary>
/// Represents <see cref="long"/> constant as type.
/// </summary>
public abstract class Int64Const : Constant<long>
{
    /// <summary>
    /// Associated <see cref="long"/> value with this type.
    /// </summary>
    /// <param name="value">A value to be associated with this type.</param>
    protected Int64Const(long value)
        : base(value)
    {
    }

    /// <summary>
    /// Represents zero value as type.
    /// </summary>
    public sealed class Zero : Int64Const
    {
        /// <summary>
        /// Represents constant value.
        /// </summary>
        public new const long Value = 0;

        /// <summary>
        /// Initializes a new constant value.
        /// </summary>
        public Zero()
            : base(Value)
        {
        }
    }

    /// <summary>
    /// Represents max long value as type.
    /// </summary>
    public sealed class Max : Int64Const
    {
        /// <summary>
        /// Represents constant value.
        /// </summary>
        public new const long Value = long.MaxValue;

        /// <summary>
        /// Initializes a new constant value.
        /// </summary>
        public Max()
            : base(Value)
        {
        }
    }

    /// <summary>
    /// Represents min long value as type.
    /// </summary>
    public sealed class Min : Int64Const
    {
        /// <summary>
        /// Represents constant value.
        /// </summary>
        public new const long Value = long.MinValue;

        /// <summary>
        /// Initializes a new constant value.
        /// </summary>
        public Min()
            : base(Value)
        {
        }
    }
}