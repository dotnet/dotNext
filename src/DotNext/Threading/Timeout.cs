using System;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Timestamp = Diagnostics.Timestamp;

    /// <summary>
    /// Helps to compute timeout for asynchronous operations.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Timeout
    {
        private readonly Timestamp created;

        // null means that this timeout is infinite
        private readonly TimeSpan? timeout;

        /// <summary>
        /// Constructs a new timeout control object.
        /// </summary>
        /// <param name="timeout">Max duration of operation.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        public Timeout(TimeSpan timeout)
        {
            created = Timestamp.Current;
            if (timeout == InfiniteTimeSpan)
                this.timeout = null;
            else if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            else
                this.timeout = timeout;
        }

        /// <summary>
        /// Gets value of this timeout.
        /// </summary>
        public TimeSpan Value => timeout ?? InfiniteTimeSpan;

        /// <summary>
        /// Determines whether this timeout is infinite.
        /// </summary>
        public bool IsInfinite => timeout is null;

        /// <summary>
        /// Indicates that timeout is occurred.
        /// </summary>
        public bool IsExpired
            => this.timeout.TryGetValue(out var timeout) && created.Elapsed > timeout;

        /// <summary>
        /// Throws <see cref="TimeoutException"/> if timeout occurs.
        /// </summary>
        public void ThrowIfExpired()
        {
            if (IsExpired)
                throw new TimeoutException();
        }

        /// <summary>
        /// Throws <see cref="TimeoutException"/> if timeout occurs.
        /// </summary>
        /// <param name="remaining">The remaining time before timeout.</param>
        public void ThrowIfExpired(out TimeSpan remaining)
        {
            if (!RemainingTime.TryGetValue(out remaining))
                throw new TimeoutException();
        }

        /// <summary>
        /// Gets the remaining time.
        /// </summary>
        /// <value>The remaining time; or <see langword="null"/> if timeout occurs.</value>
        public TimeSpan? RemainingTime
        {
            get
            {
                if (this.timeout.TryGetValue(out var timeout))
                {
                    timeout -= created.Elapsed;
                    return timeout >= TimeSpan.Zero ? new TimeSpan?(timeout) : null;
                }

                return InfiniteTimeSpan;
            }
        }

        /// <summary>
        /// Indicates that timeout is reached.
        /// </summary>
        /// <param name="timeout">Timeout control object.</param>
        /// <returns><see langword="true"/>, if timeout is reached; otherwise, <see langword="false"/>.</returns>
        public static bool operator true(in Timeout timeout) => timeout.IsExpired;

        /// <summary>
        /// Indicates that timeout is not reached.
        /// </summary>
        /// <param name="timeout">Timeout control object.</param>
        /// <returns><see langword="false"/>, if timeout is not reached; otherwise, <see langword="false"/>.</returns>
        public static bool operator false(in Timeout timeout) => !timeout.IsExpired;

        /// <summary>
        /// Extracts original timeout value from this object.
        /// </summary>
        /// <param name="timeout">Timeout control object.</param>
        /// <returns>The original timeout value.</returns>
        public static implicit operator TimeSpan(in Timeout timeout) => timeout.Value;
    }
}
