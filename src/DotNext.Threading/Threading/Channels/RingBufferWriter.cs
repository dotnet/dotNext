#if !NETSTANDARD2_1
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using Collections.Concurrent;
    using static Reflection.TaskType;

    internal sealed class RingBufferWriter<T> : ChannelWriter<T>
    {
        private readonly RingBuffer<T> buffer;
        private int readyToWrite;  // volatile, the number of available slots for write
        private volatile TaskCompletionSource consumedEvent;
        private AtomicBoolean completed;

        internal RingBufferWriter(RingBuffer<T> buffer)
        {
            this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            readyToWrite = buffer.Capacity;
            consumedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
            consumedEvent.SetResult();
            completed = new(false);
        }

        internal void Subscribe()
        {
            buffer.Consumed += OnConsumed;
            buffer.Published += OnPublished;
        }

        private void OnConsumed()
        {
            var currentEvent = consumedEvent;

            if (readyToWrite.IncrementAndGet() == 1)
                currentEvent.TrySetResult();
        }

        private void OnPublished()
        {
            if (readyToWrite.DecrementAndGet() == 0)
                consumedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override bool TryComplete(Exception? error = null)
        {
            if (completed.FalseToTrue())
            {
                buffer.Consumed -= OnConsumed;
                buffer.Published -= OnPublished;
                consumedEvent.TrySetException(error ?? new ChannelClosedException());
                return buffer.FindHandler<RingBufferReader<T>>()?.TryComplete(error) ?? false;
            }

            return false;
        }

        public override bool TryWrite(T item)
            => completed.Value ? throw new ChannelClosedException() : buffer.TryAdd(item);

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken token = default)
        {
            ValueTask<bool> result;
            if (completed.Value)
                result = new(false);
            else if (readyToWrite.VolatileRead() > 0)
                result = new(true);
            else
                result = new(consumedEvent.Task.ContinueWith<bool>(IsCompletedSuccessfullyGetter, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));

            return result;
        }
    }
}
#endif