using System.Collections;
using System.Collections.Frozen;
using System.Numerics;

namespace DotNext.Collections.Generic;

using Specialized;

/// <summary>
/// Represents various extension methods for sets.
/// </summary>
public static class Set
{
    /// <summary>
    /// Creates a range using the specified bounds.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the range.</typeparam>
    /// <typeparam name="TLowerBound">The type of lower bound.</typeparam>
    /// <typeparam name="TUpperBound">The type of upper bound.</typeparam>
    /// <param name="lowerBound">The lower bound of the range.</param>
    /// <param name="upperBound">The upper bound of the range.</param>
    /// <returns>An ordered set of elements in the range.</returns>
    public static IReadOnlySet<T> Range<T, TLowerBound, TUpperBound>(TLowerBound lowerBound, TUpperBound upperBound)
        where T : IBinaryInteger<T>
        where TLowerBound : IFiniteRangeEndpoint<T>
        where TUpperBound : IFiniteRangeEndpoint<T>
    {
        var (minValue, maxValue) = GetMinMaxValues(lowerBound, upperBound);

        return minValue.CompareTo(maxValue) switch
        {
            > 0 => FrozenSet<T>.Empty,
            0 => Singleton(minValue),
            < 0 => new RangeSet<T>(minValue, maxValue),
        };

        static (T MinValue, T MaxValue) GetMinMaxValues(TLowerBound lowerBound, TUpperBound upperBound)
        {
            var minValue = lowerBound.IsOnRight(lowerBound.Value)
                ? lowerBound.Value
                : lowerBound.Value + T.One;

            var maxValue = upperBound.IsOnLeft(upperBound.Value)
                ? upperBound.Value
                : upperBound.Value - T.One;

            return (minValue, maxValue);
        }
    }

    /// <summary>
    /// Constructs read-only set with a single item in it.
    /// </summary>
    /// <param name="item">An item to be placed into set.</param>
    /// <typeparam name="T">Type of set items.</typeparam>
    /// <returns>Read-only set containing single item.</returns>
    public static IReadOnlySet<T> Singleton<T>(T item)
        => new SingletonList<T> { Item = item };

    private sealed class RangeSet<T>(T lowerBound, T upperBound) : IReadOnlySet<T>
        where T : IBinaryInteger<T>
    {
        private readonly T lowerBound = lowerBound;
        private readonly T upperBound = upperBound;

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
                SingletonList<T> => false,
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