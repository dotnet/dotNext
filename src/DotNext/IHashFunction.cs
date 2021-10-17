using System.Runtime.InteropServices;

namespace DotNext;

/// <summary>
/// Represents accumulator.
/// </summary>
/// <typeparam name="TValue">The type of the value to accumulate.</typeparam>
/// <typeparam name="TResult">The accumulation result.</typeparam>
public interface IAccumulator<in TValue, out TResult>
{
    /// <summary>
    /// Adds the data to the hash function.
    /// </summary>
    /// <param name="input">The input data.</param>
    void Add(TValue input);

    /// <summary>
    /// Gets the result of the hash function.
    /// </summary>
    TResult Result { get; }
}

[StructLayout(LayoutKind.Auto)]
internal struct Accumulator<TValue, TResult> : IAccumulator<TValue, TResult>
{
    private readonly Func<TValue, TResult, TResult> accumulator;
    private TResult result;

    internal Accumulator(Func<TValue, TResult, TResult> accumulator, TResult initial)
    {
        this.accumulator = accumulator ?? throw new ArgumentNullException(nameof(accumulator));
        result = initial;
    }

    public void Add(TValue data) => result = accumulator(data, result);

    public TResult Result => result;
}