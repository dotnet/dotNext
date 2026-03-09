namespace DotNext.Threading;

/// <summary>
/// Provides extension for <see cref="Timeout"/> type.
/// </summary>
public static class TimeoutExtensions
{
    /// <summary>
    /// Extends <see cref="Timeout"/> type.
    /// </summary>
    /// <param name="timeout">The timeout value.</param>
    extension(in Timeout timeout)
    {
        /// <summary>
        /// Indicates that the timeout is occurred.
        /// </summary>
        /// <param name="provider">The time provider.</param>
        /// <returns><see langword="true"/> if timeout is occurred; otherwise, <see langword="false"/>.</returns>
        public bool IsExpired(TimeProvider provider) => timeout.IsExpiredInternal(provider);

        /// <summary>
        /// Gets the remaining time, or <see cref="TimeSpan.Zero"/> if timeout is occurred.
        /// </summary>
        public TimeSpan RemainingTime
        {
            get
            {
                if (!timeout.TryGetRemainingTime(out var remainingTime))
                    remainingTime = TimeSpan.Zero;

                return remainingTime;
            }
        }
    }
}