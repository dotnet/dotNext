using System.Runtime.InteropServices;

namespace DotNext;

/// <summary>
/// Represents hash function.
/// </summary>
/// <typeparam name="TInput">The type of the input.</typeparam>
/// <typeparam name="TOuput">The type of the hash result.</typeparam>
public interface IHashFunction<in TInput, out TOuput>
{
    /// <summary>
    /// Adds the data to the hash function.
    /// </summary>
    /// <param name="input">The input data.</param>
    void Add(TInput input);

    /// <summary>
    /// Gets the result of the hash function.
    /// </summary>
    TOuput Result { get; }
}

[StructLayout(LayoutKind.Auto)]
internal struct HashFunction<TInput, TOutput> : IHashFunction<TInput, TOutput>
{
    private readonly Func<TInput, TOutput, TOutput> hashFunction;
    private TOutput result;

    internal HashFunction(Func<TInput, TOutput, TOutput> hashFunction, TOutput initial)
    {
        this.hashFunction = hashFunction ?? throw new ArgumentNullException(nameof(hashFunction));
        result = initial;
    }

    public void Add(TInput data) => result = hashFunction(data, result);

    public TOutput Result => result;
}