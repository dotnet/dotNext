namespace DotNext.Generic;

/// <summary>
/// Represents boolean constant as generic parameter.
/// </summary>
public abstract class BooleanConst : Constant<bool>
{
    private BooleanConst(bool value)
        : base(value)
    {
    }

    /// <summary>
    /// Represents <see langword="true"/> constant value as generic parameter.
    /// </summary>
    public sealed class True : BooleanConst
    {
        /// <summary>
        /// Initializes a new constant value.
        /// </summary>
        public True()
            : base(true)
        {
        }
    }

    /// <summary>
    /// Represents <see langword="false"/> constant value as generic parameter.
    /// </summary>
    public sealed class False : BooleanConst
    {
        /// <summary>
        /// Initializes a new constant value.
        /// </summary>
        public False()
            : base(false)
        {
        }
    }
}