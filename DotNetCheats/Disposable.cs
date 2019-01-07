using System;

namespace Cheats
{
	using Threading;

	/// <summary>
	/// Provides implementation of dispose pattern.
	/// </summary>
	/// <see cref="IDisposable"/>
	/// <seealso cref="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose"/>
	public abstract class Disposable : IDisposable
	{
		private AtomicBoolean disposed = new AtomicBoolean(false);

		/// <summary>
		/// Indicates that this object is disposed.
		/// </summary>
		protected bool IsDisposed => disposed.Value;

		/// <summary>
		/// Throws exception if this object is disposed.
		/// </summary>
		/// <exception cref="ObjectDisposedException">Object is disposed.</exception>
		protected void ThrowIfDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(GetType().Name);
		}

		protected abstract void Dispose(bool disposing);

		private void DisposeCore(bool disposing)
		{
			if (disposed.FalseToTrue())
				Dispose(disposing);
		}

		/// <summary>
		/// Releases all resources associated with this object.
		/// </summary>
		public void Dispose()
		{
			DisposeCore(true);
			GC.SuppressFinalize(this);
		}

		~Disposable() => DisposeCore(false);
	}
}
