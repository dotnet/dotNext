using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct DelegatingMemoryConsumer<T, TArg> : ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>
    {
        private readonly Func<TArg, ReadOnlyMemory<T>, CancellationToken, ValueTask> func;
        private readonly TArg arg;

        internal DelegatingMemoryConsumer(Func<TArg, ReadOnlyMemory<T>, CancellationToken, ValueTask> func, TArg arg)
        {
            this.func = func ?? throw new ArgumentNullException(nameof(func));
            this.arg = arg;
        }

        ValueTask ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<T> input, CancellationToken token)
            => func(arg, input, token);
    }

    internal sealed class SkippingConsumer : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        internal static readonly SkippingConsumer Instance = new ();

        private SkippingConsumer()
        {
        }

        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
            => new ValueTask(token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask);
    }
}