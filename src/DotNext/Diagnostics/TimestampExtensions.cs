namespace DotNext.Diagnostics;

/// <summary>
/// Extends <see cref="Timestamp"/> type.
/// </summary>
public static class TimestampExtensions
{
    /// <summary>
    /// Extends <see cref="Timestamp"/> type.
    /// </summary>
    /// <param name="ts">The object to extend.</param>
    extension(Timestamp ts)
    {
        /// <summary>
        /// Gets a value indicating that the current timestamp represents the future point in time.
        /// </summary>
        /// <param name="provider">The time provider.</param>
        /// <returns><see langword="true"/> if the current timestamp represents the future point in time; otherwise, <see langword="false"/>.</returns>
        public bool IsFuture(TimeProvider provider) => ts.IsFutureInternal(provider);

        /// <summary>
        /// Gets a value indicating that the current timestamp represents the past point in time.
        /// </summary>
        /// <param name="provider">The time provider.</param>
        /// <returns><see langword="true"/> if the current timestamp represents the past point in time; otherwise, <see langword="false"/>.</returns>
        public bool IsPast(TimeProvider provider) => ts.IsPastInternal(provider);
    }
}