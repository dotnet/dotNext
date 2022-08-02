using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Debug = System.Diagnostics.Debug;

namespace DotNext;

using ByteBuffer = Buffers.MemoryRental<byte>;

/// <summary>
/// Provides random data generation.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Represents randomly chosen salt for hash code algorithms.
    /// </summary>
    internal static readonly int BitwiseHashSalt = Random.Shared.Next();

    private static void NextCharsCore(Random rng, ReadOnlySpan<char> allowedChars, Span<char> buffer)
    {
        Debug.Assert(rng is not null);
        Debug.Assert(!buffer.IsEmpty);
        Debug.Assert(!allowedChars.IsEmpty);

        ref var firstChar = ref MemoryMarshal.GetReference(allowedChars);
        foreach (ref var element in buffer)
            element = Unsafe.Add(ref firstChar, rng.Next(0, allowedChars.Length));
    }

    [SkipLocalsInit]
    private static void NextCharsCore(RandomNumberGenerator rng, ReadOnlySpan<char> allowedChars, Span<char> buffer)
    {
        Debug.Assert(rng is not null);
        Debug.Assert(!buffer.IsEmpty);
        Debug.Assert(!allowedChars.IsEmpty);

        var offset = buffer.Length << 2;
        using ByteBuffer bytes = (uint)offset <= (uint)ByteBuffer.StackallocThreshold ? stackalloc byte[offset] : new ByteBuffer(offset);
        rng.GetBytes(bytes.Span);
        offset = 0;
        ref var firstChar = ref MemoryMarshal.GetReference(allowedChars);
        foreach (ref var element in buffer)
        {
            var randomNumber = (uint)BitConverter.ToInt32(bytes.Span.Slice(offset)) % (uint)allowedChars.Length;
            element = Unsafe.Add(ref firstChar, randomNumber);
            offset += sizeof(int);
        }
    }

    /// <summary>
    /// Generates random string of the given length.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    /// <returns>Randomly generated string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    public static string NextString(this Random random, ReadOnlySpan<char> allowedChars, int length)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        string result;
        if (length is 0 || allowedChars.IsEmpty)
        {
            result = string.Empty;
        }
        else
        {
            result = new string('\0', length);
            NextCharsCore(random, allowedChars, MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference<char>(result), length));
        }

        return result;
    }

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
    /// Generates random set of characters.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="buffer">The array to be filled with random characters.</param>
    public static void NextChars(this Random random, ReadOnlySpan<char> allowedChars, Span<char> buffer)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!allowedChars.IsEmpty && !buffer.IsEmpty)
            NextCharsCore(random, allowedChars, buffer);
    }

    /// <summary>
    /// Generates random string of the given length.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    /// <returns>Randomly generated string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    public static string NextString(this RandomNumberGenerator random, ReadOnlySpan<char> allowedChars, int length)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        string result;
        if (length is 0 || allowedChars.IsEmpty)
        {
            result = string.Empty;
        }
        else
        {
            result = new string('\0', length);
            NextCharsCore(random, allowedChars, MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference<char>(result), length));
        }

        return result;
    }

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
    /// Generates random set of characters.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="buffer">The array to be filled with random characters.</param>
    public static void NextChars(this RandomNumberGenerator random, ReadOnlySpan<char> allowedChars, Span<char> buffer)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!allowedChars.IsEmpty && !buffer.IsEmpty)
            NextCharsCore(random, allowedChars, buffer);
    }

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