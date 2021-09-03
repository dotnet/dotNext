using System;
using System.Runtime.CompilerServices;
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
            Semaphore,
        }

        /// <summary>
        /// Represents acquired lock holder.
        /// </summary>
        /// <remarks>
        /// The lock can be released by calling <see cref="Dispose()"/>.
        /// </remarks>
        [StructLayout(LayoutKind.Auto)]
        public ref struct Holder
        {
            private readonly object lockedObject;
            private readonly Type type;

            internal Holder(object lockedObject, Type type)
            {
                this.lockedObject = lockedObject;
                this.type = type;
            }

            /// <summary>
            /// Indicates that this object doesn't hold the lock.
            /// </summary>
            public readonly bool IsEmpty => lockedObject is null;

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
            public static implicit operator bool(in Holder holder) => holder.lockedObject is not null;
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
        public static Lock Semaphore(SemaphoreSlim semaphore) => new(semaphore ?? throw new ArgumentNullException(nameof(semaphore)), Type.Semaphore, false);

        /// <summary>
        /// Creates semaphore-based lock but doesn't acquire the lock.
        /// </summary>
        /// <remarks>
        /// Constructed lock owns the semaphore instance.
        /// </remarks>
        /// <param name="initialCount">The initial number of requests for the semaphore that can be granted concurrently.</param>
        /// <param name="maxCount">The maximum number of requests for the semaphore that can be granted concurrently.</param>
        /// <returns>The lock representing semaphore.</returns>
        public static Lock Semaphore(int initialCount, int maxCount) => new(new SemaphoreSlim(initialCount, maxCount), Type.Semaphore, true);

        /// <summary>
        /// Creates monitor-based lock control object but doesn't acquire the lock.
        /// </summary>
        /// <param name="obj">Monitor lock target.</param>
        /// <returns>The lock representing monitor.</returns>
        public static Lock Monitor(object obj) => new(obj ?? throw new ArgumentNullException(nameof(obj)), Type.Monitor, false);

        /// <summary>
        /// Creates exclusive lock.
        /// </summary>
        /// <remarks>
        /// Constructed lock owns the object instance used as a monitor.
        /// </remarks>
        /// <returns>The exclusive lock.</returns>
        public static Lock Monitor() => new(new object(), Type.Monitor, true);

        /// <summary>
        /// Creates read lock but doesn't acquire it.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <param name="upgradeable"><see langword="true"/> to create upgradeable read lock wrapper.</param>
        /// <returns>Reader lock.</returns>
        public static Lock ReadLock(ReaderWriterLockSlim rwLock, bool upgradeable)
            => new(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), upgradeable ? Type.UpgradeableReadLock : Type.ReadLock, false);

        /// <summary>
        /// Creates write lock but doesn't acquire it.
        /// </summary>
        /// <param name="rwLock">Read/write lock source.</param>
        /// <returns>Write-only lock.</returns>
        public static Lock WriteLock(ReaderWriterLockSlim rwLock)
            => new(rwLock ?? throw new ArgumentNullException(nameof(rwLock)), Type.WriteLock, false);

        /// <summary>
        /// Acquires lock.
        /// </summary>
        /// <returns>The holder of the acquired lock.</returns>
        public readonly Holder Acquire()
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

        private readonly bool TryAcquire() => type switch
        {
            Type.Monitor => System.Threading.Monitor.TryEnter(lockedObject),
            Type.ReadLock => As<ReaderWriterLockSlim>(lockedObject).TryEnterReadLock(0),
            Type.WriteLock => As<ReaderWriterLockSlim>(lockedObject).TryEnterWriteLock(0),
            Type.UpgradeableReadLock => As<ReaderWriterLockSlim>(lockedObject).TryEnterUpgradeableReadLock(0),
            Type.Semaphore => As<SemaphoreSlim>(lockedObject).Wait(0),
            _ => false,
        };

        /// <summary>
        /// Attempts to acquire lock.
        /// </summary>
        /// <param name="holder">The lock holder that can be used to release acquired lock.</param>
        /// <returns><see langword="true"/>, if lock is acquired successfully; otherwise, <see langword="false"/>.</returns>
        public readonly bool TryAcquire(out Holder holder)
        {
            if (TryAcquire())
            {
                holder = new Holder(lockedObject, type);
                return true;
            }

            holder = default;
            return false;
        }

        private readonly bool TryAcquire(TimeSpan timeout) => type switch
        {
            Type.Monitor => System.Threading.Monitor.TryEnter(lockedObject, timeout),
            Type.ReadLock => As<ReaderWriterLockSlim>(lockedObject).TryEnterReadLock(timeout),
            Type.WriteLock => As<ReaderWriterLockSlim>(lockedObject).TryEnterWriteLock(timeout),
            Type.UpgradeableReadLock => As<ReaderWriterLockSlim>(lockedObject).TryEnterUpgradeableReadLock(timeout),
            Type.Semaphore => As<SemaphoreSlim>(lockedObject).Wait(timeout),
            _ => false,
        };

        /// <summary>
        /// Attempts to acquire lock.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <param name="holder">The lock holder that can be used to release acquired lock.</param>
        /// <returns><see langword="true"/>, if lock is acquired successfully; otherwise, <see langword="false"/>.</returns>
        public readonly bool TryAcquire(TimeSpan timeout, out Holder holder)
        {
            if (TryAcquire(timeout))
            {
                holder = new Holder(lockedObject, type);
                return true;
            }

            holder = default;
            return false;
        }

        /// <summary>
        /// Acquires lock.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for the lock.</param>
        /// <returns>The holder of the acquired lock.</returns>
        /// <exception cref="TimeoutException">The lock has not been acquired during the specified timeout.</exception>
        public readonly Holder Acquire(TimeSpan timeout)
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

        private readonly bool Equals(in Lock other)
            => type == other.type && ReferenceEquals(lockedObject, other.lockedObject) && owner == other.owner;

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        public readonly bool Equals(Lock other) => Equals(in other);

        /// <summary>
        /// Determines whether this lock object is the same as other lock.
        /// </summary>
        /// <param name="other">Other lock to compare.</param>
        /// <returns><see langword="true"/> if this lock is the same as the specified lock; otherwise, <see langword="false"/>.</returns>
        public override readonly bool Equals(object? other) => other is Lock @lock && Equals(in @lock);

        /// <summary>
        /// Computes hash code of this lock.
        /// </summary>
        /// <returns>The hash code of this lock.</returns>
        public override readonly int GetHashCode() => HashCode.Combine(lockedObject, type, owner);

        /// <summary>
        /// Returns actual type of this lock in the form of the string.
        /// </summary>
        /// <returns>The actual type of this lock.</returns>
        public override readonly string ToString() => type.ToString();

        /// <summary>
        /// Determines whether two locks are the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in Lock first, in Lock second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether two locks are not the same.
        /// </summary>
        /// <param name="first">The first lock to compare.</param>
        /// <param name="second">The second lock to compare.</param>
        /// <returns><see langword="true"/>, if both are not the same; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in Lock first, in Lock second)
            => !first.Equals(in second);
    }
}