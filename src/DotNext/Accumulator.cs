using System.Runtime.InteropServices;

namespace DotNext;

[StructLayout(LayoutKind.Auto)]
internal struct Accumulator<TValue, TResult> : IConsumer<TValue>, ISupplier<TResult>
{
    private readonly Func<TValue, TResult, TResult> accumulator;
    private TResult result;

    internal Accumulator(Func<TValue, TResult, TResult> accumulator, TResult initial)
    {
        this.accumulator = accumulator ?? throw new ArgumentNullException(nameof(accumulator));
        result = initial;
    }

    public void Invoke(TValue input) => result = accumulator(input, result);

    public TResult Invoke() => result;
}