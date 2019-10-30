using System;
using System.Runtime.InteropServices;

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
        private readonly TimeSpan timeout;

        /// <summary>
        /// Constructs a new timeout control object.
        /// </summary>
        /// <param name="timeout">Max duration of operation.</param>
        public Timeout(TimeSpan timeout)
        {
            created = Timestamp.Current;
            this.timeout = timeout;
        }

        /// <summary>
        /// Indicates that timeout is reached.
        /// </summary>
        public bool IsExpired => created.Elapsed > timeout;

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
            remaining = timeout - created.Elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException();
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
		public static implicit operator TimeSpan(in Timeout timeout) => timeout.timeout;
    }
}
