using System;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents a synchronization primitive that is signaled when its count reaches zero.
    /// </summary>
    /// <remarks>
    /// This is asynchronous version of <see cref="System.Threading.CountdownEvent"/>.
    /// </remarks>
    public class AsyncCountdownEvent : Synchronizer, IAsyncEvent
    {
        private sealed class CounterNode : ISynchronizer.WaitNode
        {
            private long count;

            internal CounterNode(long count) => this.count = count;

            internal long Count => count;

            internal void AddCount(long value) => count += value;

            internal bool Signal(long value)
            {
                if ((count -= value) <= 0L)
                {
                    Complete();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Creates a new countdown event with the specified count.
        /// </summary>
        /// <param name="initialCount">The number of signals initially required to set the event.</param>
        public AsyncCountdownEvent(long initialCount)
        {
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCount));
            InitialCount = initialCount;

            // just 1 node needed to implement countdown event at all
            if (initialCount > 0L)
                node = new CounterNode(initialCount);
        }

        /// <summary>
        /// Gets the numbers of signals initially required to set the event.
        /// </summary>
        public long InitialCount { get; private set; }

        /// <summary>
        /// Gets the number of remaining signals required to set the event.
        /// </summary>
        public long CurrentCount => node is CounterNode counter ? counter.Count : 0L;

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal bool TryAddCount(long signalCount, bool autoReset)
        {
            ThrowIfDisposed();
            if (signalCount < 0)
                throw new ArgumentOutOfRangeException(nameof(signalCount));

            if (node is CounterNode counter)
            {
                counter.AddCount(signalCount);
                return true;
            }

            if (autoReset)
            {
                node = new CounterNode(signalCount);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to increment the current count by a specified value.
        /// </summary>
        /// <param name="signalCount">The value by which to increase <see cref="CurrentCount"/>.</param>
        /// <returns><see langword="true"/> if the increment succeeded; if <see cref="CurrentCount"/> is already at zero this will return <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="signalCount"/> is less than zero.</exception>
        public bool TryAddCount(long signalCount) => TryAddCount(signalCount, false);

        /// <summary>
        /// Attempts to increment the current count by one.
        /// </summary>
        /// <returns><see langword="true"/> if the increment succeeded; if <see cref="CurrentCount"/> is already at zero this will return <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public bool TryAddCount() => TryAddCount(1L);

        /// <summary>
        /// Increments the current count by a specified value.
        /// </summary>
        /// <param name="signalCount">The value by which to increase <see cref="CurrentCount"/>.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="signalCount"/> is less than zero.</exception>
        /// <exception cref="InvalidOperationException">The current instance is already set.</exception>
        public void AddCount(long signalCount)
        {
            if (!TryAddCount(signalCount))
                throw new InvalidOperationException();
        }

        /// <summary>
        /// Increments the current count by one.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="InvalidOperationException">The current instance is already set.</exception>
        public void AddCount() => AddCount(1L);

        /// <summary>
        /// Resets the <see cref="CurrentCount"/> to the value of <see cref="InitialCount"/>.
        /// </summary>
        /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public bool Reset() => Reset(InitialCount);

        /// <summary>
        /// Resets the <see cref="InitialCount"/> property to a specified value.
        /// </summary>
        /// <param name="count">The number of signals required to set this event.</param>
        /// <returns><see langword="true"/>, if state of this object changed from signaled to non-signaled state; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Reset(long count)
        {
            ThrowIfDisposed();
            if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));

            // in signaled state
            if (node is null)
            {
                node = count > 0L ? new CounterNode(count) : null;
                InitialCount = count;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal bool Signal(long signalCount, bool autoReset)
        {
            ThrowIfDisposed();
            if (signalCount < 1L)
                throw new ArgumentOutOfRangeException(nameof(signalCount));
            var node = this.node as CounterNode;
            if (node is null) // already in signaled state
                return false;

            if (node.Count == 0L || signalCount > node.Count)
                throw new InvalidOperationException();

            // complete all awaiters
            if (node.Signal(signalCount))
            {
                this.node = autoReset ? new CounterNode(InitialCount) : null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Registers multiple signals with this object, decrementing the value of <see cref="CurrentCount"/> by the specified amount.
        /// </summary>
        /// <param name="signalCount">The number of signals to register.</param>
        /// <returns><see langword="true"/> if the signals caused the count to reach zero and the event was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="signalCount"/> is less than 1.</exception>
        /// <exception cref="InvalidOperationException">The current instance is already set; or <paramref name="signalCount"/> is greater than <see cref="CurrentCount"/>.</exception>
        public bool Signal(long signalCount) => Signal(signalCount, false);

        /// <summary>
        /// Registers multiple signals with this object, decrementing the value of <see cref="CurrentCount"/> by one.
        /// </summary>
        /// <returns><see langword="true"/> if the signals caused the count to reach zero and the event was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="InvalidOperationException">The current instance is already set.</exception>
        public bool Signal() => Signal(1L);
    }
}