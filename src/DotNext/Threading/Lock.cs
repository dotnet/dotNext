using System;
using System.Runtime.InteropServices;
using System.Threading;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading
{
    /// <summary>
    /// Unified representation of monitor lock, semaphore lock, read lock, write lock or upgradeable read lock.
    /// </summary>
    /// <remarks>
    /// The lock acquisition may block the caller thread.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public struct Lock : IDisposable, IEquatable<Lock>
    {
        internal enum Type : byte
        {
            None = 0,
            Monitor,
            ReadLock,
            UpgradeableReadLock,
            WriteLock,
            Semaphore
        }

        /// <summary>
        /// Represents acquired lock holder.
        /// </summary>
        /// <remarks>
        /// The lock can be released by calling <see cref="Dispose()"/>.
        /// </remarks>
        public struct Holder : IDisposable   //TODO: Should be ref struct but Dispose pattern is not working in C# 7.x
        {
            private readonly object lockedObject;
            private readonly Type type;

            internal Holder(object lockedObject, Type type)
            {
                this.lockedObject = lockedObject;
                this.type = type;
            }

            /// <summary>
            /// Releases the acquired lock.
            /// </summary>
            /// <remarks>
            /// This object is not reusable after calling of this method.
            /// </remarks>
            public void Dispose()
            {
                switch (type)
                {
                    case Type.Monitor:
                        System.Threading.Monitor.Exit(lockedObject);
                        break;
                    case Type.ReadLock:
                        As<ReaderWriterLockSlim>(lockedObject).ExitReadLock();
                        break;
                    case Type.WriteLock:
                        As<ReaderWriterLockSlim>(lockedObject).ExitWriteLock();
                        break;
                    case Type.UpgradeableReadLock:
                        As<ReaderWriterLockSlim>(lockedObject).ExitUpgradeableReadLock();
                        break;
                    case Type.Semaphore:
                        As<SemaphoreSlim>(lockedObject).Release();
                        break;
                }
                this = default;
            }

            /// <summary>
            /// Indicates that the object holds successfully acquired lock.
            /// </summary>
            /// <param name="holder">The lock holder.</param>
            /// <returns><see langword="true"/>, if the object holds successfully acquired lock; otherwise, <see langword="false"/>.</returns>
            public static bool operator true(in Holder holder) => !(holder.lockedObject is null);

            /// <summary>
            /// Indicates that the object doesn't hold the lock.
            /// </summary>
            /// <param name="holder">The lock holder.</param>
            /// <returns><see langword="false"/>, if the object holds successfully acquired lock; otherwise, <see langword="true"/>.</returns>
            public static bool operator false(in Holder holder) => holder.lockedObject is null;
        }

        private readonly object lockedObject;
        private readonly Type type;
        private readonly bool owner;

        private Lock(object lockedObject, Type type, bool owner)
        {
            this.lockedObject = lockedObject;
            this.type = type;
            this.owner = owner;
        }

        /// <summary>
        /// Wraps semaphore instance into the unified representation of the lock.
        /// </summary>
        /// <param name="semaphore">The semaphore to wrap into lock object.</param>
        /// <returns>The lock representing semaphore.</returns>
        public static Lock Semaphore(SemaphoreSlim semaphore) => new Lock(semaphore ?? throw new ArgumentNullException(nameof(semaphore)), Type.Semaphore, false);

        /// <summary>
        /// Creates semaphore-based lock but doesn't acquire the lock.
        /// </summary>
        /// <remarks>
        /// Constructed lock owns the semaphore instance.
        /// </remarks>
        /// <param name="initialCount">The initial number of requests for the semaphore that can be granted concurrently.</param>
        /// <param name="maxCount">The maximum number of requests for the semaphore that can be granted concurrently.</param>
        /// <returns>The lock representing semaphore.</returns>
        public static Lock Semaphore(int initialCount, int maxCount) => new Lock(new SemaphoreSlim(initialCount, maxCount), Type.Semaphore, true);

        /// <summary>
        /// Creates monitor-based lock control object but doesn't acquire the lock.
        /// </summary>
        /// <param name="obj">Monitor lock target.</param>
        /// <returns>The lock representing monitor.</returns>
        public static Lock Monitor(object obj) => new Lock(obj ?? throw new ArgumentNullException(nameof(obj)), Type.Monitor, false);

        /// <summary>
        /// Creates exclusive lock.
        /// </summary>
        /// <remarks>
        /// Constructed lock owns the object instance used as a monitor.
        /// </remarks>
        /// <returns>The exclusive lock.</returns>
        public static Lock Monitor() => new Lock(new object(), Type.Monitor, true);

        /// <summary>
        /// Creates read lock but doesn't acquire it.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <param name="upgradeable"><see langword="true"/> to create upgradeable read lock wrapper.</param>
        /// <returns>Reader lock.</returns>
        public static Lock ReadLock(ReaderWriterLockSlim rwLock, bool upgradeable)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), upgradeable ? Type.UpgradeableReadLock : Type.ReadLock, false);

        /// <summary>
        /// Creates write lock but doesn't acquire it.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <returns>Write-only lock.</returns>
        public static Lock WriteLock(ReaderWriterLockSlim rwLock)
            => new Lock(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), Type.WriteLock, false);

        /// <summary>
        /// Acquires lock.
        /// </summary>
        public Holder Acquire()
        {
            switch (type)
            {
                case Type.Monitor:
                    System.Threading.Monitor.Enter(lockedObject);
                    break;
                case Type.ReadLock:
                    As<ReaderWriterLockSlim>(lockedObject).EnterReadLock();
                    break;
                case Type.WriteLock:
                    As<ReaderWriterLockSlim>(lockedObject).EnterWriteLock();
                    break;
                case Type.UpgradeableReadLock:
                    As<ReaderWriterLockSlim>(lockedObject).EnterUpgradeableReadLock();
                    break;
                case Type.Semaphore:
                    As<SemaphoreSlim>(lockedObject).Wait();
                    break;
            }
            return new Holder(lockedObject, type);
        }

        private bool TryAcquire()
        {
            switch (type)
            {
                case Type.Monitor:
                    return System.Threading.Monitor.TryEnter(lockedObject);
                case Type.ReadLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterReadLock(0);
                case Type.WriteLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterWriteLock(0);
                case Type.UpgradeableReadLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterUpgradeableReadLock(0);
                case Type.Semaphore:
                    return As<SemaphoreSlim>(lockedObject).Wait(0);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Attempts to acquire lock.
        /// </summary>
        /// <param name="holder">The lock holder that can be used to release acquired lock.</param>
        /// <returns><see langword="true"/>, if lock is acquired successfully; otherwise, <see langword="false"/></returns>
        public bool TryAcquire(out Holder holder)
        {
            if (TryAcquire())
            {
                holder = new Holder(lockedObject, type);
                return true;
            }
            else
            {
                holder = default;
                return false;
            }
        }

        private bool TryAcquire(TimeSpan timeout)
        {
            switch (type)
            {
                case Type.Monitor:
                    return System.Threading.Monitor.TryEnter(lockedObject, timeout);
                case Type.ReadLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterReadLock(timeout);
                case Type.WriteLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterWriteLock(timeout);
                case Type.UpgradeableReadLock:
                    return As<ReaderWriterLockSlim>(lockedObject).TryEnterUpgradeableReadLock(timeout);
                case Type.Semaphore:
                    return As<SemaphoreSlim>(lockedObject).Wait(timeout);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Attempts to acquire lock.
        /// </summary>
        /// <param name="holder">The lock holder that can be used to release acquired lock.</param>
        /// <param name="timeout">The amount of time to wait for the lock</param>
        /// <returns><see langword="true"/>, if lock is acquired successfully; otherwise, <see langword="false"/></returns>
        public bool TryAcquire(TimeSpan timeout, out Holder holder)
        {
            if (TryAcquire(timeout))
            {
                holder = new Holder(lockedObject, type);
                return true;
            }
            else
            {
                holder = default;
                return false;
            }
        }

        /// <summary>
        /// Acquires lock.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for the lock</param>
        /// <exception cref="TimeoutException">The lock has not been acquired during the specified timeout.</exception>
        public Holder Acquire(TimeSpan timeout)
            => TryAcquire(timeout) ? new Holder(lockedObject, type) : throw new TimeoutException();

        /// <summary>
        /// Destroy this lock and dispose underlying lock object if it is owned by the given lock.
        /// </summary>
        /// <remarks>
        /// If the given lock is an owner of the underlying lock object then this method will call <see cref="IDisposable.Dispose"/> on it;
        /// otherwise, the underlying lock object will not be destroyed.
        /// As a result, this lock is not usable after calling of this method.
        /// </remarks>
        public void Dispose()
        {
            if (owner && lockedObject is IDisposable disposable)
                disposable.Dispose();
            this = default;
        }

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        public bool Equals(Lock other) => type == other.type && ReferenceEquals(lockedObject, other.lockedObject) && owner == other.owner;

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is Lock @lock && Equals(@lock);

        /// <summary>
        /// Computes hash code of this lock.
        /// </summary>
        /// <returns>The hash code of this lock.</returns>
        public override int GetHashCode()
        {
            if (lockedObject is null)
                return 0;
            var hashCode = -549183179;
            hashCode = hashCode * -1521134295 + lockedObject.GetHashCode();
            hashCode = hashCode * -1521134295 + type.GetHashCode();
            hashCode = hashCode * -1521134295 + owner.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// Returns actual type of this lock in the form of the string.
        /// </summary>
        /// <returns>The actual type of this lock.</returns>
        public override string ToString() => type.ToString();

        /// <summary>
        /// Determines whether two locks are the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in Lock first, in Lock second) => ReferenceEquals(first.lockedObject, second.lockedObject) && first.type == second.type && first.owner == second.owner;

        /// <summary>
        /// Determines whether two locks are not the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are not the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in Lock first, in Lock second) => !ReferenceEquals(first.lockedObject, second.lockedObject) || first.type != second.type || first.owner != second.owner;
    }
}