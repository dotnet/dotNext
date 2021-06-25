#if !NETSTANDARD2_1
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using Collections.Concurrent;
    using Runtime.ExceptionServices;
    using static Reflection.TaskType;

    /// <summary>
    /// Represents asynchronous consumer of <see cref="RingBuffer{T}"/>.
    /// </summary>
    /// <remarks>
    /// This reader supports single and multiple consumers.
    /// </remarks>
    /// <typeparam name="T">The type of elements in the buffer.</typeparam>
    internal sealed class RingBufferReader<T> : ChannelReader<T>
    {
        private readonly RingBuffer<T> buffer;
        private readonly TaskCompletionSource completed;
        private volatile TaskCompletionSource publishedEvent;

        private int readyToRead; // volatile, the number of items to consume

        /// <summary>
        /// Initializes a new asynchronous reader for the ring buffer.
        /// </summary>
        /// <param name="buffer">The ring buffer to be consumed asynchronously.</param>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
        internal RingBufferReader(RingBuffer<T> buffer)
        {
            this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            publishedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
            completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <inheritdoc/>
        public override Task Completion => completed.Task;

        internal bool TryComplete(Exception? error)
        {
            if (error is null ? completed.TrySetResult() : completed.TrySetException(error))
            {
                buffer.Published -= OnPublished;
                buffer.Consumed -= OnConsumed;
                publishedEvent.TrySetException(error ?? new ChannelClosedException());
                return true;
            }

            return false;
        }

        internal void Subscribe()
        {
            buffer.Published += OnPublished;
            buffer.Consumed += OnConsumed;
        }

        /// <summary>
        /// Stops receiving elements from the buffer.
        /// </summary>
        public void Unsubscribe() => TryComplete(null);

        private void OnConsumed()
        {
            if (readyToRead.DecrementAndGet() == 0)
                publishedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private void OnPublished()
        {
            var currentEvent = publishedEvent;

            // turn event into signaled state
            if (readyToRead.IncrementAndGet() == 1)
                currentEvent.TrySetResult();
        }

        /// <summary>
        /// Always returns <see langword="true"/>.
        /// </summary>
        public override bool CanCount => true;

        /// <summary>
        /// Gets approximate number of items in the buffer available to read.
        /// </summary>
        public override int Count => readyToRead.VolatileRead();

        /// <summary>
        /// Attempts to obtain the element from the buffer synchronously.
        /// </summary>
        /// <param name="item">The consumed element from the buffer.</param>
        /// <returns><see langword="true"/> if the element consumed successfully; otherwise, <see langword="false"/>.</returns>
        public override bool TryRead([MaybeNullWhen(false)] out T item)
            => buffer.TryRemove(out item) || completed.Task.Status switch
        {
            TaskStatus.RanToCompletion => throw new ChannelClosedException(),
            TaskStatus.Faulted => throw completed.Task.Exception!.GetFirstException(),
            _ => false,
        };

        /// <inheritdoc/>
        public override ValueTask<bool> WaitToReadAsync(CancellationToken token = default)
        {
            return readyToRead.VolatileRead() > 0 ? new(true) : completed.Task.Status switch
            {
                TaskStatus.RanToCompletion => new(false),
                TaskStatus.Faulted => ValueTask.FromException<bool>(completed.Task.Exception!.GetFirstException()),
                _ => new(publishedEvent.Task.ContinueWith<bool>(IsCompletedSuccessfullyGetter, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)),
            };
        }
    }
}
#endif