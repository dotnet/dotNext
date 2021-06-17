using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks;

    public partial class AsyncCorrelationSource<TKey, TValue>
    {
        /// <summary>
        /// Indicates that <see cref="Listener"/> object has been disposed
        /// or constructed incorrectly.
        /// </summary>
        public sealed class ListenerNoLongerValidException : Exception
        {
            internal ListenerNoLongerValidException()
                : base(ExceptionMessages.ListenerNoLongerValid)
            {
            }
        }

        /// <summary>
        /// Represents correlation event listener.
        /// </summary>
        /// <remarks>
        /// The listener must be destroyed when no longer needed with <see cref="Dispose()"/> method.
        /// </remarks>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct Listener : IDisposable
        {
            private readonly LinkedListNode<Slot>? slotHolder;

            internal Listener(LinkedListNode<Slot> slotHolder)
            {
                this.slotHolder = slotHolder;
            }

            /// <summary>
            /// Waits for the signal.
            /// </summary>
            /// <returns>The task representing the signal.</returns>
            public Task<TValue> WaitAsync() => slotHolder?.Value?.Task ?? Task.FromException<TValue>(new ListenerNoLongerValidException());

            /// <summary>
            /// Waits for the signal.
            /// </summary>
            /// <param name="timeout">The time to wait for the signal.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The task representing the signal.</returns>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
            /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
            /// <exception cref="TimeoutException">The timeout has occurred.</exception>
            public Task<TValue> WaitAsync(TimeSpan timeout, CancellationToken token = default)
                => WaitAsync().ContinueWithTimeout(timeout, token);

            /// <summary>
            /// Cancels listening.
            /// </summary>
            public void Dispose()
            {
                var list = slotHolder?.List;
                if (list is not null)
                {
                    Debug.Assert(slotHolder is not null);

                    lock (list)
                    {
                        list.Remove(slotHolder);
                    }
                }
            }
        }
    }
}