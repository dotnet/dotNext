using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DotNext;

using Buffers;

/// <summary>
/// Provides random data generation.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Represents randomly chosen salt for hash code algorithms.
    /// </summary>
    internal static readonly int BitwiseHashSalt = Random.Shared.Next();

    private static readonly bool CleanupInternalBuffer = !LibrarySettings.DisableRandomStringInternalBufferCleanup;

    private interface IRandomBytesSource
    {
        void GetBytes(Span<byte> bytes);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct RandomBytesSource : IRandomBytesSource
    {
        private readonly Random random;

        internal RandomBytesSource(Random random)
            => this.random = random;

        void IRandomBytesSource.GetBytes(Span<byte> bytes) => random.NextBytes(bytes);

        public static implicit operator RandomBytesSource(Random r) => new(r);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct CryptographicRandomBytesSource : IRandomBytesSource
    {
        private readonly RandomNumberGenerator random;

        internal CryptographicRandomBytesSource(RandomNumberGenerator random)
            => this.random = random;

        void IRandomBytesSource.GetBytes(Span<byte> bytes) => random.GetBytes(bytes);

        public static implicit operator CryptographicRandomBytesSource(RandomNumberGenerator r) => new(r);
    }

    [StructLayout(LayoutKind.Auto)]
    private ref struct CachedRandomNumberGenerator<TRandom>
        where TRandom : struct, IRandomBytesSource
    {
        private SpanReader<byte> reader;
        private TRandom randomBytesSource;

        internal CachedRandomNumberGenerator(TRandom random, Span<byte> randomVector)
        {
            randomBytesSource = random;
            reader = new(randomVector);
        }

        private uint NextUInt32()
        {
            uint result;
            while (!reader.TryRead(out result))
            {
                reader.Reset();
                randomBytesSource.GetBytes(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(reader.Span), reader.RemainingCount));
            }

            return result;
        }

        internal uint NextUInt32(uint maxValue)
        {
            // Algorithm: https://arxiv.org/pdf/1805.10941.pdf
            var m = (ulong)NextUInt32() * maxValue;
            var low = (uint)m;

            if (low < maxValue)
            {
                for (var t = unchecked(~maxValue + 1U) % maxValue; low < t; low = (uint)m)
                {
                    m = (ulong)NextUInt32() * maxValue;
                }
            }

            return (uint)(m >> 32);
        }
    }

    private static void NextChars<TRandom>(TRandom random, ReadOnlySpan<char> allowedChars, Span<char> buffer)
        where TRandom : struct, IRandomBytesSource
    {
        Debug.Assert(!buffer.IsEmpty);
        Debug.Assert(!allowedChars.IsEmpty);

        var randomBufLength = buffer.Length << 2;
        using MemoryRental<byte> bytes = (uint)randomBufLength <= (uint)MemoryRental<byte>.StackallocThreshold
            ? stackalloc byte[randomBufLength]
            : new MemoryRental<byte>(randomBufLength);
        random.GetBytes(bytes.Span);

        if (BitOperations.IsPow2(allowedChars.Length))
        {
            // optimized branch, we can avoid modulo operation at all and have an unbiased version
            NextCharsFast(bytes.Span, allowedChars, buffer);
        }
        else
        {
            var cache = new CachedRandomNumberGenerator<TRandom>(random, bytes.Span);
            NextChars(ref cache, allowedChars, buffer);
        }

        if (CleanupInternalBuffer)
            bytes.Span.Clear();
    }

    private static void NextCharsFast(ReadOnlySpan<byte> randomVector, ReadOnlySpan<char> allowedChars, Span<char> output)
    {
        Debug.Assert(BitOperations.IsPow2(allowedChars.Length));
        Debug.Assert(randomVector.Length == output.Length * 4);

        // x % 2^n == x & (2^n - 1) and we know that Length == 2^n
        var moduloOperand = (uint)(allowedChars.Length - 1);

        ref var firstChar = ref MemoryMarshal.GetReference(allowedChars);
        foreach (ref var outputChar in output)
        {
            var charPos = BitConverter.ToUInt32(randomVector) & moduloOperand;
            outputChar = Unsafe.Add(ref firstChar, charPos);
            randomVector = randomVector.Slice(sizeof(uint));
        }
    }

    private static void NextChars<TRandom>(scoped ref CachedRandomNumberGenerator<TRandom> cache, ReadOnlySpan<char> allowedChars, Span<char> output)
        where TRandom : struct, IRandomBytesSource
    {
        ref var firstChar = ref MemoryMarshal.GetReference(allowedChars);
        foreach (ref var outputChar in output)
        {
            var charPos = cache.NextUInt32((uint)allowedChars.Length);
            outputChar = Unsafe.Add(ref firstChar, charPos);
        }
    }

    /// <summary>
    /// Generates random string of the given length.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    /// <returns>Randomly generated string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
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
            result = new('\0', length);
            NextChars<RandomBytesSource>(random, allowedChars, MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in result.GetPinnableReference()), length));
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
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    public static string NextString(this Random random, string allowedChars, int length)
        => NextString(random, allowedChars.AsSpan(), length);

    /// <summary>
    /// Generates random set of characters.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="buffer">The array to be filled with random characters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    public static void NextChars(this Random random, ReadOnlySpan<char> allowedChars, Span<char> buffer)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!allowedChars.IsEmpty && !buffer.IsEmpty)
            NextChars<RandomBytesSource>(random, allowedChars, buffer);
    }

    /// <summary>
    /// Generates random string of the given length.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    /// <returns>Randomly generated string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
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
            NextChars<CryptographicRandomBytesSource>(random, allowedChars, MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in result.GetPinnableReference()), length));
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
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    public static string NextString(this RandomNumberGenerator random, string allowedChars, int length)
        => NextString(random, allowedChars.AsSpan(), length);

    /// <summary>
    /// Generates random set of characters.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="allowedChars">The allowed characters for the random string.</param>
    /// <param name="buffer">The array to be filled with random characters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    public static void NextChars(this RandomNumberGenerator random, ReadOnlySpan<char> allowedChars, Span<char> buffer)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!allowedChars.IsEmpty && !buffer.IsEmpty)
            NextChars<CryptographicRandomBytesSource>(random, allowedChars, buffer);
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
    /// <returns>A 32-bit signed integer that is in range [0, <see cref="int.MaxValue"/>).</returns>
    public static int Next(this RandomNumberGenerator random)
    {
        const uint maxValue = uint.MaxValue >> 1;
        Unsafe.SkipInit(out uint result);

        do
        {
            random.GetBytes(Span.AsBytes(ref result));
            result >>= 1; // remove sign bit
        }
        while (result is maxValue);

        return (int)result;
    }

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
        random.GetBytes(Span.AsBytes(ref result));
        return result;
    }
}