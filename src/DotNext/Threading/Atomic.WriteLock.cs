using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial struct Atomic<T>
{
    /// <summary>
    /// Enters write lock.
    /// </summary>
    /// <returns>The scope of the write lock.</returns>
    [UnscopedRef]
    public WriteLockScope EnterLock() => new(ref this);
    
    /// <summary>
    /// Represents scope of the write lock.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct WriteLockScope : IDisposable
    {
        private readonly nuint stamp;
        private readonly ref Atomic<T> atomic;

        internal WriteLockScope(ref Atomic<T> atomic)
        {
            this.atomic = ref atomic;
            stamp = atomic.EnterWriteLock();
        }

        /// <summary>
        /// Gets a value that can be modified within the scope safely.
        /// </summary>
        public ref T Value => ref atomic.value;

        /// <summary>
        /// Releases write lock.
        /// </summary>
        public void Dispose() => atomic.ExitWriteLock(stamp);
    }
}