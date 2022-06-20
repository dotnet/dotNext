using System.Runtime.InteropServices;

namespace DotNext;

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