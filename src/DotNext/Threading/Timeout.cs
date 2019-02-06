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
		/// <returns>True, if timeout is reached; otherwise, false.</returns>
		public static bool operator true(Timeout timeout) => timeout.Expired;

		public static bool operator false(Timeout timeout) => !timeout.Expired;

		public static implicit operator TimeSpan(Timeout timeout) => timeout.timeout;
	}
}
