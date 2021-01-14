using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    public interface IReadOnlySpanConsumer<T>
    {
        void Invoke(ReadOnlySpan<T> span);
    }

    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ReadOnlySpanConsumer<T, TArg> : IReadOnlySpanConsumer<T>
    {
        private readonly delegate*<ReadOnlySpan<T>, TArg, void> ptr;
        private readonly TArg arg;

        public ReadOnlySpanConsumer(delegate*<ReadOnlySpan<T>, TArg, void> ptr, TArg arg)
        {
            this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;
            this.arg = arg;
        }

        /// <inheritdoc />
        void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> span) => ptr(span, arg);
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelegatingReadOnlySpanConsumer<T, TArg> : IReadOnlySpanConsumer<T>
    {
        private readonly ReadOnlySpanAction<T, TArg> action;
        private readonly TArg arg;

        public DelegatingReadOnlySpanConsumer(ReadOnlySpanAction<T, TArg> action, TArg arg)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            this.arg = arg;
        }

        /// <inheritdoc />
        void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> span) => action(span, arg);
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct BufferConsumer<T> : IReadOnlySpanConsumer<T>
    {
        private readonly IBufferWriter<T> output;

        public BufferConsumer(IBufferWriter<T> output) => this.output = output ?? throw new ArgumentNullException(nameof(output));

        /// <inheritdoc />
        void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> span) => output.Write(span);
    }
}