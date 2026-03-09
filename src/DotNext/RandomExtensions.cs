using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DotNext;

using Numerics;
using Runtime.InteropServices;

/// <summary>
/// Provides random data generation.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Represents randomly chosen salt for hash code algorithms.
    /// </summary>
    internal static readonly int BitwiseHashSalt = Random.Shared.Next();

    /// <summary>
    /// Extends <see cref="RandomNumberGenerator"/> type.
    /// </summary>
    extension(RandomNumberGenerator)
    {
        /// <summary>
        /// Generates non-negative integer.
        /// </summary>
        /// <returns>A 32-bit signed integer that is in range [0, <see cref="int.MaxValue"/>).</returns>
        public static int Next()
        {
            const uint maxValue = uint.MaxValue >>> 1;
            Unsafe.SkipInit(out uint result);

            do
            {
                RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(ref result));
                result >>>= 1; // remove sign bit
            }
            while (result is maxValue);

            return (int)result;
        }
        
        /// <summary>
        /// Generates a random boolean value.
        /// </summary>
        /// <param name="trueProbability">A probability of <see langword="true"/> result (should be between 0.0 and 1.0).</param>
        /// <returns>Randomly generated boolean value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="trueProbability"/> value is invalid.</exception>
        public static bool NextBoolean(double trueProbability = 0.5D)
            => trueProbability is >= 0D and <= 1D ?
                RandomNumberGenerator.NextDouble() >= 1.0D - trueProbability :
                throw new ArgumentOutOfRangeException(nameof(trueProbability));

        /// <summary>
        /// Returns a random floating-point number that is in range [0, 1).
        /// </summary>
        /// <returns>Randomly generated floating-point number.</returns>
        public static double NextDouble()
            => RandomNumberGenerator.Next<ulong>().Normalize();
        
        /// <summary>
        /// Generates a random value of blittable type.
        /// </summary>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>The randomly generated value.</returns>
        [SkipLocalsInit]
        public static T Next<T>()
            where T : unmanaged
        {
            Unsafe.SkipInit(out T result);
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(ref result));
            return result;
        }
    }

    /// <param name="random">The source of random numbers.</param>
    extension(Random random)
    {
        /// <summary>
        /// Generates a random boolean value.
        /// </summary>
        /// <param name="trueProbability">A probability of <see langword="true"/> result (should be between 0.0 and 1.0).</param>
        /// <returns>Randomly generated boolean value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="trueProbability"/> value is invalid.</exception>
        public bool NextBoolean(double trueProbability = 0.5D)
            => trueProbability is >= 0D and <= 1D ?
                random.NextDouble() >= 1.0D - trueProbability :
                throw new ArgumentOutOfRangeException(nameof(trueProbability));

        /// <summary>
        /// Generates a random value of blittable type.
        /// </summary>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>The randomly generated value.</returns>
        [SkipLocalsInit]
        public T Next<T>()
            where T : unmanaged
        {
            Unsafe.SkipInit(out T result);
            random.NextBytes(MemoryMarshal.AsBytes(ref result));
            return result;
        }

        /// <summary>
        /// Randomizes elements in the list.
        /// </summary>
        /// <typeparam name="T">The type of items in the list.</typeparam>
        /// <param name="list">The list to shuffle.</param>
        public void Shuffle<T>(IList<T> list)
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
        /// <param name="collection">The collection to get the random element.</param>
        /// <returns>The random element from the collection.</returns>
        /// <exception cref="ArgumentException"><paramref name="collection"/> is empty.</exception>
        public T Peek<T>(IReadOnlyCollection<T> collection)
        {
            ArgumentException.ThrowIfNullOrEmpty(collection);
            
            return collection switch
            {
                { Count: 1 } => collection.ElementAt(0),
                T[] array => random.PeekCore(array.AsSpan()),
                List<T> list => random.PeekCore(CollectionsMarshal.AsSpan(list)),
                _ => PeekRandomSlow(random, collection),
            };

            [MethodImpl(MethodImplOptions.NoInlining)]
            static T PeekRandomSlow(Random random, IReadOnlyCollection<T> collection)
            {
                var index = random.Next(collection.Count);
                using var enumerator = collection.GetEnumerator();
                for (var i = 0; enumerator.MoveNext(); i++)
                {
                    if (i == index)
                        return enumerator.Current;
                }

                throw ArgumentException.EmptyCollection(nameof(collection));
            }
        }

        /// <summary>
        /// Chooses the random element in the span.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span of elements.</param>
        /// <returns>Randomly selected element from the span.</returns>
        /// <exception cref="ArgumentException"><paramref name="span"/> is empty.</exception>
        public T Peek<T>(ReadOnlySpan<T> span)
        {
            ArgumentException.ThrowIfEmpty(span);

            return random.PeekCore(span);
        }

        private T PeekCore<T>(ReadOnlySpan<T> span)
        {
            var length = span.Length;
            return length is 1
                ? MemoryMarshal.GetReference(span)
                : span[random.Next(length)];
        }
    }
}