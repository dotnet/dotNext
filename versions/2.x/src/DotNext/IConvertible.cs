namespace DotNext
{
    /// <summary>
    /// Represents common interface for objects that support explicit conversion to the particular type.
    /// </summary>
    /// <typeparam name="T">The type of conversion result.</typeparam>
    public interface IConvertible<out T>
    {
        /// <summary>
        /// Converts this instance into another type.
        /// </summary>
        /// <returns>The conversion result.</returns>
        T Convert();
    }
}
