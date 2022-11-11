using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO;

using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<byte>;

public static partial class StreamSource
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct ReadOnlySpanWriter<TArg> : IReadOnlySpanConsumer, IFlushable
    {
        private readonly ReadOnlySpanAction<byte, TArg> output;
        private readonly TArg arg;
        private readonly Action<TArg>? flush;
        private readonly Func<TArg, CancellationToken, Task>? flushAsync;

        internal ReadOnlySpanWriter(ReadOnlySpanAction<byte, TArg> output, TArg arg, Action<TArg>? flush, Func<TArg, CancellationToken, Task>? flushAsync)
        {
            this.output = output;
            this.arg = arg;
            this.flush = flush;
            this.flushAsync = flushAsync;
        }

        void IFlushable.Flush() => Flush(flush, flushAsync, arg);

        Task IFlushable.FlushAsync(CancellationToken token) => FlushAsync(flush, flushAsync, arg, token);

        void IReadOnlySpanConsumer.Invoke(scoped ReadOnlySpan<byte> input) => output(input, arg);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ReadOnlyMemoryWriter<TArg> : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, IFlushable
    {
        private readonly Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> output;
        private readonly TArg arg;
        private readonly Action<TArg>? flush;
        private readonly Func<TArg, CancellationToken, Task>? flushAsync;

        internal ReadOnlyMemoryWriter(Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> output, TArg arg, Action<TArg>? flush, Func<TArg, CancellationToken, Task>? flushAsync)
        {
            this.output = output;
            this.arg = arg;
            this.flush = flush;
            this.flushAsync = flushAsync;
        }

        void IFlushable.Flush() => Flush(flush, flushAsync, arg);

        Task IFlushable.FlushAsync(CancellationToken token) => FlushAsync(flush, flushAsync, arg, token);

        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
            => output(input, arg, token);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct BufferWriter<TBuffer> : IReadOnlySpanConsumer, IFlushable
        where TBuffer : class, IBufferWriter<byte>
    {
        private readonly TBuffer output;
        private readonly Action<TBuffer>? flush;
        private readonly Func<TBuffer, CancellationToken, Task>? flushAsync;

        internal BufferWriter(TBuffer output, Action<TBuffer>? flush, Func<TBuffer, CancellationToken, Task>? flushAsync)
        {
            this.output = output;
            this.flush = flush;
            this.flushAsync = flushAsync;
        }

        void IFlushable.Flush() => Flush(flush, flushAsync, output);

        Task IFlushable.FlushAsync(CancellationToken token) => FlushAsync(flush, flushAsync, output, token);

        void IReadOnlySpanConsumer.Invoke(scoped ReadOnlySpan<byte> input) => output.Write(input);
    }

    // should be used if TBuffer is IReadOnlySpanConsumer
    [StructLayout(LayoutKind.Auto)]
    private readonly struct DelegatingWriter<TBuffer> : IReadOnlySpanConsumer, IFlushable
        where TBuffer : class, IBufferWriter<byte>
    {
        private readonly TBuffer output;
        private readonly Action<TBuffer>? flush;
        private readonly Func<TBuffer, CancellationToken, Task>? flushAsync;

        internal DelegatingWriter(TBuffer output, Action<TBuffer>? flush, Func<TBuffer, CancellationToken, Task>? flushAsync)
        {
            Debug.Assert(output is IReadOnlySpanConsumer);
            this.output = output;
            this.flush = flush;
            this.flushAsync = flushAsync;
        }

        void IFlushable.Flush() => Flush(flush, flushAsync, output);

        Task IFlushable.FlushAsync(CancellationToken token) => FlushAsync(flush, flushAsync, output, token);

        void IReadOnlySpanConsumer.Invoke(scoped ReadOnlySpan<byte> input) => Unsafe.As<IReadOnlySpanConsumer>(output).Invoke(input);
    }

    private static void Flush<TArg>(Action<TArg>? flush, Func<TArg, CancellationToken, Task>? flushAsync, TArg arg)
    {
        if (flush is null)
        {
            if (flushAsync is not null)
                flushAsync(arg, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        else
        {
            flush(arg);
        }
    }

    private static Task FlushAsync<TArg>(Action<TArg>? flush, Func<TArg, CancellationToken, Task>? flushAsync, TArg arg, CancellationToken token)
    {
        if (flushAsync is null)
        {
            return flush is null ?
                Task.CompletedTask
                : Task.Factory.StartNew(CreateTaskCallback(flush, arg), token, TaskCreationOptions.None, TaskScheduler.Current);
        }

        return flushAsync(arg, token);

        static Action CreateTaskCallback(Action<TArg> action, TArg arg) => new(() => action(arg));
    }
}