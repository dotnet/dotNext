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
        private readonly ulong maxValue;
        private SpanReader<byte> reader;
        private TRandom randomBytesSource;

        internal CachedRandomNumberGenerator(TRandom random, Span<byte> randomVector, uint maxValue)
        {
            this.maxValue = maxValue;
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

        internal nuint NextOffset()
        {
            // Algorithm: https://arxiv.org/pdf/1805.10941.pdf
            var m = NextUInt32() * maxValue;
            var low = (uint)m;

            if (low < maxValue)
            {
                for (var t = unchecked(~maxValue + 1U) % maxValue; low < t; low = (uint)m)
                {
                    m = NextUInt32() * maxValue;
                }
            }

            // only lower 32 bit contains useful information, cast to int32 or int64 is safe
            return (nuint)(m >> 32);
        }
    }

    [SkipLocalsInit]
    private static void Next<TRandom, T>(TRandom random, ReadOnlySpan<T> allowedInput, Span<T> buffer)
        where TRandom : struct, IRandomBytesSource
    {
        Debug.Assert(!buffer.IsEmpty);
        Debug.Assert(!allowedInput.IsEmpty);

        var randomBufLength = buffer.Length << 2;
        using MemoryRental<byte> bytes = (uint)randomBufLength <= (uint)MemoryRental<byte>.StackallocThreshold
            ? stackalloc byte[randomBufLength]
            : new MemoryRental<byte>(randomBufLength);
        random.GetBytes(bytes.Span);

        var allowedInputLength = (uint)allowedInput.Length;
        if (BitOperations.IsPow2(allowedInputLength))
        {
            // optimized branch, we can avoid modulo operation at all and have an unbiased version
            FastPath(
                ref MemoryMarshal.GetReference(bytes.Span),
                ref MemoryMarshal.GetReference(allowedInput),
                allowedInputLength - 1U, // x % 2^n == x & (2^n - 1) and we know that Length == 2^n
                buffer);
        }
        else
        {
            var cache = new CachedRandomNumberGenerator<TRandom>(random, bytes.Span, allowedInputLength);
            NextCore(
                ref cache,
                ref MemoryMarshal.GetReference(allowedInput),
                buffer);
        }

        if (CleanupInternalBuffer)
            bytes.Span.Clear();

        static void FastPath(ref byte randomVectorPtr, ref T inputPtr, nuint moduloOperand, Span<T> output)
        {
            Debug.Assert(BitOperations.IsPow2(moduloOperand + 1));

            foreach (ref var outputPtr in output)
            {
                outputPtr = Unsafe.Add(ref inputPtr, Unsafe.ReadUnaligned<uint>(ref randomVectorPtr) & moduloOperand);
                randomVectorPtr = ref Unsafe.Add(ref randomVectorPtr, sizeof(uint));
            }
        }

        static void NextCore(scoped ref CachedRandomNumberGenerator<TRandom> cache, ref T inputPtr, Span<T> output)
        {
            foreach (ref var outputChar in output)
            {
                outputChar = Unsafe.Add(ref inputPtr, cache.NextOffset());
            }
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
            Next<RandomBytesSource, char>(random, allowedChars, AllocString(length, out result));
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
            Next<RandomBytesSource, char>(random, allowedChars, buffer);
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
            Next<CryptographicRandomBytesSource, char>(random, allowedChars, AllocString(length, out result));
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<char> AllocString(int length, out string result)
        => MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in (result = new('\0', length)).GetPinnableReference()), length);

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
            Next<CryptographicRandomBytesSource, char>(random, allowedChars, buffer);
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
    public static T Next<T>(this Random random)
        where T : unmanaged
    {
        Unsafe.SkipInit(out T result);
        random.NextBytes(Span.AsBytes(ref result));
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