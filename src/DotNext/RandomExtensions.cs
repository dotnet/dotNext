using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DotNext;

using ByteBuffer = Buffers.MemoryRental<byte>;
using CharBuffer = Buffers.MemoryRental<char>;

/// <summary>
/// Provides random data generation.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Represents randomly chosen salt for hash code algorithms.
    /// </summary>
    internal static readonly int BitwiseHashSalt = Random.Shared.Next();

    private static void NextString(Random rng, Span<char> buffer, ReadOnlySpan<char> allowedChars)
    {
        ref var firstChar = ref MemoryMarshal.GetReference(allowedChars);
        foreach (ref var element in buffer)
            element = Unsafe.Add(ref firstChar, rng.Next(0, allowedChars.Length));
    }

    [SkipLocalsInit]
    private static void NextString(RandomNumberGenerator rng, Span<char> buffer, ReadOnlySpan<char> allowedChars)
    {
        var offset = buffer.Length * sizeof(int);
        using ByteBuffer bytes = (uint)offset <= ByteBuffer.StackallocThreshold ? stackalloc byte[offset] : new ByteBuffer(offset);
        rng.GetBytes(bytes.Span);
        offset = 0;
        ref var firstChar = ref MemoryMarshal.GetReference(allowedChars);
        foreach (ref var element in buffer)
        {
            var randomNumber = (BitConverter.ToInt32(bytes.Span.Slice(offset)) & int.MaxValue) % allowedChars.Length;
            element = Unsafe.Add(ref firstChar, randomNumber);
            offset += sizeof(int);
        }
    }

    [SkipLocalsInit]
    private static unsafe string NextString<TGenerator>(TGenerator generator, delegate*<TGenerator, Span<char>, ReadOnlySpan<char>, void> impl, ReadOnlySpan<char> allowedChars, int length)
        where TGenerator : class
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (length == 0 || allowedChars.IsEmpty)
            return string.Empty;

        // use stack allocation for small strings, which is 99% of all use cases
        using CharBuffer result = length <= CharBuffer.StackallocThreshold ? stackalloc char[length] : new CharBuffer(length);
        impl(generator, result.Span, allowedChars);
        return new string(result.Span);
    }

    /// <summary>
    /// Generates random string of the given length.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    /// <returns>Randomly generated string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    public static unsafe string NextString(this Random random, ReadOnlySpan<char> allowedChars, int length)
        => NextString(random, &NextString, allowedChars, length);

    /// <summary>
    /// Generates random string of the given length.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The string of allowed characters for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    /// <returns>Randomly generated string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    public static string NextString(this Random random, string allowedChars, int length)
        => NextString(random, allowedChars.AsSpan(), length);

    /// <summary>
    /// Generates random string of the given length.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    /// <returns>Randomly generated string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    public static unsafe string NextString(this RandomNumberGenerator random, ReadOnlySpan<char> allowedChars, int length)
        => NextString(random, &NextString, allowedChars, length);

    /// <summary>
    /// Generates random string of the given length.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The string of allowed characters for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    /// <returns>Randomly generated string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    public static string NextString(this RandomNumberGenerator random, string allowedChars, int length)
        => NextString(random, allowedChars.AsSpan(), length);

    /// <summary>
    /// Generates random boolean value.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="trueProbability">A probability of <see langword="true"/> result (should be between 0.0 and 1.0).</param>
    /// <returns>Randomly generated boolean value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trueProbability"/> value is invalid.</exception>
    public static bool NextBoolean(this Random random, double trueProbability = 0.5D)
        => trueProbability.IsBetween(0D, 1D, BoundType.Closed) ?
                random.NextDouble() >= 1.0D - trueProbability :
                throw new ArgumentOutOfRangeException(nameof(trueProbability));

    /// <summary>
    /// Generates random non-negative random integer.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <returns>A 32-bit signed integer that is in range [0, <see cref="int.MaxValue"/>].</returns>
    public static int Next(this RandomNumberGenerator random)
        => random.Next<int>() & int.MaxValue; // remove sign bit. Abs function may cause OverflowException

    /// <summary>
    /// Generates random boolean value.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="trueProbability">A probability of <see langword="true"/> result (should be between 0.0 and 1.0).</param>
    /// <returns>Randomly generated boolean value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trueProbability"/> value is invalid.</exception>
    public static bool NextBoolean(this RandomNumberGenerator random, double trueProbability = 0.5D)
        => trueProbability.IsBetween(0D, 1D, BoundType.Closed) ?
                random.NextDouble() >= (1.0D - trueProbability) :
                throw new ArgumentOutOfRangeException(nameof(trueProbability));

    /// <summary>
    /// Returns a random floating-point number that is in range [0, 1).
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <returns>Randomly generated floating-point number.</returns>
    public static double NextDouble(this RandomNumberGenerator random)
        => random.Next<ulong>().Normalize();

    /// <summary>
    /// Generates random value of blittable type.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <typeparam name="T">The blittable type.</typeparam>
    /// <returns>The randomly generated value.</returns>
    [SkipLocalsInit]
    public static unsafe T Next<T>(this Random random)
        where T : unmanaged
    {
        Unsafe.SkipInit(out T result);
        random.NextBytes(new Span<byte>(&result, sizeof(T)));
        return result;
    }

    /// <summary>
    /// Generates random value of blittable type.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <typeparam name="T">The blittable type.</typeparam>
    /// <returns>The randomly generated value.</returns>
    [SkipLocalsInit]
    public static unsafe T Next<T>(this RandomNumberGenerator random)
        where T : unmanaged
    {
        Unsafe.SkipInit(out T result);
        random.GetBytes(new Span<byte>(&result, sizeof(T)));
        return result;
    }
}