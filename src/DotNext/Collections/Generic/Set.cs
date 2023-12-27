using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Numerics;

namespace DotNext.Collections.Generic;

using Specialized;

/// <summary>
/// Represents various extension methods for sets.
/// </summary>
public static class Set
{
    /// <summary>
    /// Creates a range of integer values.
    /// </summary>
    /// <typeparam name="T">The type of elements in the range.</typeparam>
    /// <param name="fromInclusive">The first element in the range.</param>
    /// <param name="count">The number of elements in the range.</param>
    /// <returns>A set containing all elements in the range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is negative;
    /// or <paramref name="fromInclusive"/>+<paramref name="count"/>-<c>1</c> is larger than maximum value of type <typeparamref name="T"/>.
    /// </exception>
    public static IReadOnlySet<T> Range<T>(T fromInclusive, T count)
        where T : notnull, IBinaryInteger<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return count == T.Zero
            ? FrozenSet<T>.Empty
            : count == T.One
            ? Singleton(fromInclusive)
            : new RangeSet<T>(fromInclusive, count);
    }

    /// <summary>
    /// Constructs read-only set with a single item in it.
    /// </summary>
    /// <param name="item">An item to be placed into set.</param>
    /// <typeparam name="T">Type of set items.</typeparam>
    /// <returns>Read-only set containing single item.</returns>
    public static IReadOnlySet<T> Singleton<T>(T item)
        => new SingletonList<T> { Item = item };

    private sealed class RangeSet<T> : IReadOnlySet<T>
        where T : notnull, IBinaryInteger<T>
    {
        private readonly T lowerBound;
        private readonly T upperBound;

        internal RangeSet(T fromInclusive, T count)
        {
            Debug.Assert(!T.IsNegative(count));
            Debug.Assert(count != T.Zero);
            Debug.Assert(count != T.One);

            try
            {
                upperBound = checked(fromInclusive + (count - T.One));
            }
            catch (OverflowException e)
            {
                throw new ArgumentOutOfRangeException(nameof(count), e.Message);
            }

            lowerBound = fromInclusive;
        }

        private T Count => upperBound - lowerBound + T.One;

        int IReadOnlyCollection<T>.Count => int.CreateChecked(Count);

        public IEnumerator<T> GetEnumerator()
        {
            return GetEnumerator(lowerBound, upperBound);

            static IEnumerator<T> GetEnumerator(T fromInclusive, T toInclusive)
            {
                for (var i = fromInclusive; i <= toInclusive; i++)
                    yield return i;
            }
        }

        private bool Contains(T item)
            => item >= lowerBound && item <= upperBound;

        bool IReadOnlySet<T>.Contains(T item) => Contains(item);

        bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other)
        {
            return other switch
            {
                RangeSet<T> range => lowerBound == range.lowerBound && upperBound == range.upperBound,
                SingletonList<T> list => false,
                _ when other.TryGetNonEnumeratedCount(out var count) && count is 0 => false,
                _ => other.All(Contains),
            };
        }

        bool IReadOnlySet<T>.Overlaps(IEnumerable<T> other)
        {
            return other switch
            {
                RangeSet<T> range => range.lowerBound <= upperBound && range.upperBound >= lowerBound,
                SingletonList<T> list => list.Item >= lowerBound && list.Item <= upperBound,
                _ when other.TryGetNonEnumeratedCount(out var count) && count is 0 => true,
                _ => other.Any(Contains),
            };
        }

        bool IReadOnlySet<T>.IsSubsetOf(IEnumerable<T> other)
        {
            if (other is RangeSet<T> range)
                return lowerBound >= range.lowerBound && upperBound <= range.upperBound;

            if (other is SingletonList<T> || other.TryGetNonEnumeratedCount(out var count) && count is 0)
                return false;

            var set = other as IReadOnlySet<T> ?? new HashSet<T>(other);
            for (var i = lowerBound; i <= upperBound; i++)
            {
                if (!set.Contains(i))
                    return false;
            }

            return true;
        }

        bool IReadOnlySet<T>.IsProperSubsetOf(IEnumerable<T> other)
        {
            if (other is RangeSet<T> range)
                return lowerBound > range.lowerBound && upperBound < range.upperBound;

            if (other is SingletonList<T> || other.TryGetNonEnumeratedCount(out var count) && count is 0)
                return false;

            var set = other as IReadOnlySet<T> ?? new HashSet<T>(other);
            var matchCount = T.Zero;
            for (var i = lowerBound; i <= upperBound; i++)
            {
                if (!set.Contains(i))
                    return false;

                matchCount++;
            }

            return T.CreateChecked(set.Count) > matchCount;
        }

        bool IReadOnlySet<T>.IsSupersetOf(IEnumerable<T> other)
        {
            if (other is RangeSet<T> range)
                return lowerBound <= range.lowerBound && upperBound >= range.upperBound;

            if (other is SingletonList<T> list)
                return list.Item >= lowerBound && list.Item <= upperBound;

            if (other.TryGetNonEnumeratedCount(out var count) && count is 0)
                return true;

            foreach (var item in other)
            {
                if (item < lowerBound || item > upperBound)
                    return false;
            }

            return true;
        }

        bool IReadOnlySet<T>.IsProperSupersetOf(IEnumerable<T> other)
        {
            if (other is RangeSet<T> range)
                return lowerBound < range.lowerBound && upperBound > range.upperBound;

            if (other is SingletonList<T> list)
                return list.Item >= lowerBound && list.Item <= upperBound;

            if (other.TryGetNonEnumeratedCount(out var count) && count is 0)
                return true;

            var matchedCount = T.Zero;
            foreach (var item in other.Distinct())
            {
                if (item < lowerBound || item > upperBound)
                    return false;

                matchedCount++;
            }

            return matchedCount < Count;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"[{lowerBound}..{upperBound}]";
    }
}