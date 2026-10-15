using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Runtime;
using Runtime.CompilerServices;

partial struct SpanWriter<T>
{
    /// <summary>
    /// Represents a reference to <see cref="SpanWriter{T}"/>.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly ref struct Ref : IGrowableBuffer<T>
    {
        private readonly LocalReference<SpanWriter<T>> reference;

        /// <summary>
        /// Initializes a new reference.
        /// </summary>
        /// <param name="writer">Location of the writer.</param>
        public Ref(ref SpanWriter<T> writer)
            => reference = new(ref writer);
        
        /// <inheritdoc/>
        void IBufferWriter<T>.Advance(int count) => reference.Value.Advance(count);

        /// <inheritdoc/>
        Memory<T> IBufferWriter<T>.GetMemory(int sizeHint)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        Span<T> IBufferWriter<T>.GetSpan(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            if (sizeHint is 0)
                sizeHint = 1;

            var remainingSpan = reference.Value.RemainingSpan;
            return remainingSpan.Length >= sizeHint
                ? remainingSpan
                : throw new InsufficientMemoryException(ExceptionMessages.NotEnoughMemory);
        }

        /// <inheritdoc/>
        long IGrowableBuffer<T>.WrittenCount => reference.Value.WrittenCount;

        /// <inheritdoc/>
        void IGrowableBuffer<T>.Write(ReadOnlySpan<T> input)
            => reference.Value.Write(input);

        /// <inheritdoc/>
        void IConsumer<ReadOnlySpan<T>>.Invoke(ReadOnlySpan<T> buffer)
            => reference.Value.Write(buffer);

        /// <inheritdoc/>
        ValueTask ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<T> buffer, CancellationToken token)
        {
            ValueTask task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled(token);
            }
            else
            {
                task = ValueTask.CompletedTask;
                try
                {
                    reference.Value.Write(buffer.Span);
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }
            }

            return task;
        }

        /// <inheritdoc/>
        void IGrowableBuffer<T>.Write(T value) => reference.Value.Add() = value;

        /// <inheritdoc/>
        void IGrowableBuffer<T>.CopyTo<TConsumer>(TConsumer consumer)
            => consumer.Invoke(reference.Value.WrittenSpan);

        /// <inheritdoc/>
        ValueTask IGrowableBuffer<T>.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
            => ValueTask.FromException(new NotSupportedException());

        /// <inheritdoc/>
        int IGrowableBuffer<T>.CopyTo(Span<T> output)
            => reference.Value.WrittenSpan >>> output;

        /// <inheritdoc/>
        bool IGrowableBuffer<T>.TryGetWrittenContent(out ReadOnlyMemory<T> block)
        {
            block = default;
            return false;
        }

        /// <inheritdoc cref="IFunctional.DynamicInvoke"/>
        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(count, 1);
            
            reference.Value.Write(args.Immutable<ReadOnlySpan<T>>());
        }

        /// <inheritdoc/>
        void IDisposable.Dispose()
        {
        }

        /// <inheritdoc/>
        void IResettable.Reset() => reference.Value.Reset();
    }
}