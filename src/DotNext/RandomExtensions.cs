using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DotNext;

using UInt32LocalBuffer = Buffers.MemoryRental<uint>;

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
        private readonly Span<uint> randomBuffer;
        private uint position; // reading position within random vector
        private TRandom randomBytesSource;

        internal CachedRandomNumberGenerator(TRandom random, Span<uint> randomBuffer)
        {
            Debug.Assert(!randomBuffer.IsEmpty);

            randomBytesSource = random;
            this.randomBuffer = randomBuffer;
            position = 0U;
        }

        private uint NextUInt32()
        {
            if (position >= (uint)randomBuffer.Length)
            {
                position = 0;
                GetItems(randomBytesSource, randomBuffer);
            }

            return Unsafe.Add(ref MemoryMarshal.GetReference(randomBuffer), position++);
        }

        internal nuint NextOffset(uint maxValue)
        {
            // Algorithm: https://arxiv.org/pdf/1805.10941.pdf
            ulong m = (ulong)NextUInt32() * maxValue;

            if ((uint)m < maxValue)
                m = Reject(m, maxValue);

            // only lower 32 bit contains useful information, cast to int32 or int64 is safe
            return (nuint)(m >> 32);
        }

        // this is very unlikely execution path that prevents inlining when placed to NextOffset directly because of loop
        [MethodImpl(MethodImplOptions.NoInlining)]
        private ulong Reject(ulong m, uint maxValue)
        {
            uint t = unchecked(0U - maxValue) % maxValue;

            while ((uint)m < t)
                m = (ulong)NextUInt32() * maxValue;

            return m;
        }

        internal void Randomize<T>(ReadOnlySpan<T> input, Span<T> output)
        {
            foreach (ref var outputChar in output)
            {
                outputChar = Unsafe.Add(ref MemoryMarshal.GetReference(input), NextOffset((uint)input.Length));
            }
        }
    }

    [SkipLocalsInit]
    private static void Next<TRandom, T>(TRandom random, ReadOnlySpan<T> allowedInput, Span<T> buffer)
        where TRandom : struct, IRandomBytesSource
    {
        // TODO: Remove in .NET 8: https://github.com/dotnet/runtime/issues/73864
        Debug.Assert(!buffer.IsEmpty);
        Debug.Assert(!allowedInput.IsEmpty);

        // alloc vector of uint instead of bytes to preserve memory alignment
        using UInt32LocalBuffer randomVectorBuffer = (uint)buffer.Length <= (uint)UInt32LocalBuffer.StackallocThreshold
            ? stackalloc uint[buffer.Length]
            : new UInt32LocalBuffer(buffer.Length);
        GetItems(random, randomVectorBuffer.Span);

        if (BitOperations.IsPow2(allowedInput.Length))
        {
            // optimized branch, we can avoid modulo operation at all and have an unbiased version
            FastPath(
                ref MemoryMarshal.GetReference(randomVectorBuffer.Span),
                ref MemoryMarshal.GetReference(allowedInput),
                (uint)allowedInput.Length - 1U, // x % 2^n == x & (2^n - 1) and we know that Length == 2^n
                buffer);
        }
        else
        {
            new CachedRandomNumberGenerator<TRandom>(random, randomVectorBuffer.Span).Randomize(allowedInput, buffer);
        }

        if (CleanupInternalBuffer)
            randomVectorBuffer.Span.Clear();

        static void FastPath(ref uint randomVectorPtr, ref T inputPtr, uint moduloOperand, Span<T> output)
        {
            Debug.Assert(BitOperations.IsPow2(moduloOperand + 1U));

            foreach (ref var outputPtr in output)
            {
                outputPtr = Unsafe.Add(ref inputPtr, randomVectorPtr & moduloOperand);
                randomVectorPtr = ref Unsafe.Add(ref randomVectorPtr, 1U);
            }
        }
    }

    private static unsafe void GetItems<TRandom, T>(TRandom random, Span<T> values)
        where TRandom : struct, IRandomBytesSource
        where T : unmanaged
    {
        for (int maxLength = Array.MaxLength / sizeof(T), length; !values.IsEmpty; values = values.Slice(length))
        {
            length = Math.Min(values.Length, maxLength);
            random.GetBytes(MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(values)), length * sizeof(T)));
        }
    }

    /// <summary>
    /// Generates a random string of the given length.
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
        ArgumentOutOfRangeException.ThrowIfNegative(length);

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
    /// Generates a random string of the given length.
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
    /// Generates a random set of characters.
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
    /// Generates a random string of the given length.
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
    /// Generates a random string of the given length.
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
    /// Generates a random set of characters.
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
    /// Generates a random boolean value.
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
    /// Generates non-negative integer.
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
    /// Generates a random boolean value.
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
    /// Generates a random value of blittable type.
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
    /// Generates a random value of blittable type.
    /// </summary>
    /// <param name="random">The source of random numbers.</param>
    /// <typeparam name="T">The blittable type.</typeparam>
    /// <returns>The randomly generated value.</returns>
    [SkipLocalsInit]
    public static T Next<T>(this RandomNumberGenerator random)
        where T : unmanaged
    {
        Unsafe.SkipInit(out T result);
        random.GetBytes(Span.AsBytes(ref result));
        return result;
    }

    /// </summary>
    /// <typeparam name="T">The blittable type.</typeparam>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="buffer">The buffer to fill with random values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    public static void GetItems<T>(this Random random, Span<T> buffer)
        where T : unmanaged
        => GetItems<RandomBytesSource, T>(random ?? throw new ArgumentNullException(nameof(random)), buffer);

    /// <summary>
    /// Fills the buffer with random values of the specified type.
    /// </summary>
    /// <typeparam name="T">The blittable type.</typeparam>
    /// <param name="random">The source of random numbers.</param>
    /// <param name="buffer">The buffer to fill with random values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    public static void GetItems<T>(this RandomNumberGenerator random, Span<T> buffer)
        where T : unmanaged
        => GetItems<CryptographicRandomBytesSource, T>(random ?? throw new ArgumentNullException(nameof(random)), buffer);

    /// <summary>
    /// Randomizes elements in the list.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="random">The source of random values.</param>
    /// <param name="list">The list to shuffle.</param>
    public static void Shuffle<T>(this Random random, IList<T> list)
    {
        Span<T> span;
        switch (list)
        {
            case List<T> typedList:
                span = CollectionsMarshal.AsSpan(typedList);
                break;
            case T[] array:
                span = array;
                break;
            default:
                ShuffleSlow(random, list);
                return;
        }

        random.Shuffle(span);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ShuffleSlow(Random random, IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var randomIndex = random.Next(i + 1);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }
    }

    /// <summary>
    /// Gets the random element from the collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="random">The random numbers source.</param>
    /// <param name="collection">The collection to get the random element.</param>
    /// <returns>The random element from the collection; or <see cref="Optional{T}.None"/> if collection is empty.</returns>
    public static Optional<T> Peek<T>(this Random random, IReadOnlyCollection<T> collection)
    {
        return collection switch
        {
            null => throw new ArgumentNullException(nameof(collection)),
            { Count: 0 } => Optional<T>.None,
            { Count: 1 } => collection.ElementAt(0),
            T[] array => random.Peek<T>(array.AsSpan()),
            List<T> list => random.Peek<T>(CollectionsMarshal.AsSpan(list)),
            _ => PeekRandomSlow(random, collection),
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Optional<T> PeekRandomSlow(Random random, IReadOnlyCollection<T> collection)
        {
            var index = random.Next(collection.Count);
            using var enumerator = collection.GetEnumerator();
            for (var i = 0; enumerator.MoveNext(); i++)
            {
                if (i == index)
                    return enumerator.Current;
            }

            return Optional<T>.None;
        }
    }

    /// <summary>
    /// Chooses the random element in the span.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="random">The source of random values.</param>
    /// <param name="span">The span of elements.</param>
    /// <returns>Randomly selected element from the span; or <see cref="Optional{T}.None"/> if span is empty.</returns>
    public static Optional<T> Peek<T>(this Random random, ReadOnlySpan<T> span)
    {
        var length = span.Length;

        return length switch
        {
            0 => Optional<T>.None,
            1 => MemoryMarshal.GetReference(span),
            _ => span[random.Next(length)],
        };
    }
}