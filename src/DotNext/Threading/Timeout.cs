using System;

namespace DotNext.Threading
{
	/// <summary>
	/// Helps to compute timeout for asynchronous operations.
	/// </summary>
	public readonly struct Timeout
	{
		private readonly DateTime created;
		private readonly TimeSpan timeout;

		/// <summary>
		/// Constructs a new timeout control object.
		/// </summary>
		/// <param name="timeout">Max duration of operation.</param>
		public Timeout(TimeSpan timeout)
		{
			created = DateTime.Now;
			this.timeout = timeout;
		}

		internal bool Zero => timeout == TimeSpan.Zero;

		/// <summary>
		/// Indicates that timeout is reached.
		/// </summary>
		public bool Expired => DateTime.Now - created > timeout;

		/// <summary>
		/// Throws <see cref="TimeoutException"/> if timeout occurs.
		/// </summary>
		public void ThrowIfExpired()
		{
			if (Expired)
				throw new TimeoutException();
		}

		/// <summary>
		/// Indicates that timeout is reached.
		/// </summary>
		/// <param name="timeout">Timeout control object.</param>
		/// <returns><see langword="true"/>, if timeout is reached; otherwise, <see langword="false"/>.</returns>
		public static bool operator true(in Timeout timeout) => timeout.Expired;

        /// <summary>
        /// Indicates that timeout is not reached.
        /// </summary>
        /// <param name="timeout">Timeout control object.</param>
        /// <returns><see langword="false"/>, if timeout is not reached; otherwise, <see langword="false"/>.</returns>
        public static bool operator false(in Timeout timeout) => !timeout.Expired;

        /// <summary>
        /// Extracts original timeout value from this object.
        /// </summary>
        /// <param name="timeout">Timeout control object.</param>
        /// <returns>The original timeout value.</returns>
		public static implicit operator TimeSpan(in Timeout timeout) => timeout.timeout;
	}
}
