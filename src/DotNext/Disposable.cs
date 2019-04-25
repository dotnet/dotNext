using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DotNext
{
    using Threading;

    /// <summary>
    /// Provides implementation of dispose pattern.
    /// </summary>
    /// <see cref="IDisposable"/>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose">Implementing Dispose method</seealso>
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Releases managed and unmanaged resources associated with this object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Finalize()"/>.</param>
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

        /// <summary>
        /// Disposes many objects.
        /// </summary>
        /// <param name="objects">An array of objects to dispose.</param>
        public static void Dispose(IEnumerable<IDisposable> objects)
        {
            foreach (var obj in objects)
                obj.Dispose();
        }

        /// <summary>
        /// Disposes many objects in safe manner.
        /// </summary>
        /// <param name="objects">An array of objects to dispose.</param>
        public static void Dispose(params IDisposable[] objects)
            => Dispose((IEnumerable<IDisposable>)objects);

        /// <summary>
        /// Finalizes this object.
        /// </summary>
        ~Disposable() => DisposeCore(false);
    }
}
