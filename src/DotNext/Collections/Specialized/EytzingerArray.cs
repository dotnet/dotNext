using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Buffers;

namespace DotNext.Collections.Specialized;

/// <summary>
/// Represents an array that is sorted in a way suitable for Eytzinger Binary Search. 
/// </summary>
/// <typeparam name="T">The type of the elements in the array.</typeparam>
/// <seealso href="https://arxiv.org/pdf/1509.05053">Array layouts for comparison-based searching</seealso>
[StructLayout(LayoutKind.Auto)]
public struct EytzingerArray<T> : IDisposable
{
    private MemoryOwner<T> sorted;

    /// <summary>
    /// Initializes a new array.
    /// </summary>
    /// <param name="input">The array sorted in ascending order.</param>
    /// <param name="allocator">The allocator to be used to copy <paramref name="input"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="input"/> is empty or too large.</exception>
    public EytzingerArray(ReadOnlySpan<T> input, MemoryAllocator<T>? allocator = null)
        : this(input, comparer: null, allocator)
    {
    }

    internal EytzingerArray(ReadOnlySpan<T> input, Comparison<T>? comparer, MemoryAllocator<T>? allocator)
    {
        string errorMessage;
        switch (input.Length)
        {
            case 0:
                errorMessage = ExceptionMessages.SmallBuffer;
                break;
            case int.MaxValue:
                errorMessage = ExceptionMessages.LargeBuffer;
                break;
            default:
                sorted = allocator.AllocateExactly(input.Length + 1);
                if (comparer is not null)
                {
                    PrepareCore(input, sorted.Span, comparer, allocator);
                }
                else
                {
                    PrepareCore(input, sorted.Span);
                }

                return;
        }

        throw new ArgumentException(errorMessage, nameof(input));
    }

    /// <summary>
    /// The sorted sequence of elements.
    /// </summary>
    /// <param name="input">The sorted sequence in ascending order.</param>
    /// <param name="output">
    /// The output buffer to be filled with an elements from <paramref name="input"/> in an order that is required
    /// by Eytzinger Binary Search algorithm.
    /// </param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    public static void Prepare(ReadOnlySpan<T> input, Span<T> output)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(input.Length, output.Length - 1, nameof(output));

        PrepareCore(input, output);
    }

    private static int PrepareCore(ReadOnlySpan<T> input, Span<T> output, int i = 0, int k = 1)
    {
        for (int nk; k <= input.Length; k = nk + 1)
        {
            nk = 2 * k;
            i = PrepareCore(input, output, i, nk);
            output[k] = input[i++];
        }

        return i;
    }

    private static void PrepareCore(ReadOnlySpan<T> input, Span<T> output, Comparison<T> comparer, MemoryAllocator<T>? allocator)
    {
        using var temp = allocator.AllocateExactly(input.Length);
        input.CopyTo(temp.Span);
        temp.Span.Sort(comparer);
        PrepareCore(temp.Span, output);
    }

    private static int FindIndex<TItem>(ReadOnlySpan<T> sortedInput, TItem item)
        where TItem : ISupplier<T, int>
    {
        var k = 1;
        while (k < sortedInput.Length)
        {
            k = 2 * k + Unsafe.BitCast<bool, byte>(item.Invoke(sortedInput[k]) >= 0);
        }

        return k;
    }

    /// <summary>
    /// Finds a value in the sorted array that is greater than or equal
    /// to the specified item.
    /// </summary>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <returns>The value that is greater than <paramref name="item"/>; or <see cref="Optional{T}.None"/> if the upper bound doesn't exist.</returns>
    public readonly Optional<T> FindUpperBound<TItem>(TItem item)
        where TItem : ISupplier<T, int>
        => FindUpperBound(sorted.Span, item);

    /// <summary>
    /// Finds a value in the sorted array (prepared by <see cref="Prepare"/>) that is greater than or equal
    /// to the specified item.
    /// </summary>
    /// <param name="sortedInput">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <returns>The value that is greater than <paramref name="item"/>; or <see cref="Optional{T}.None"/> if the upper bound doesn't exist.</returns>
    public static Optional<T> FindUpperBound<TItem>(ReadOnlySpan<T> sortedInput, TItem item)
        where TItem : ISupplier<T, int>
    {
        var index = GetUpperBoundIndex(FindIndex(sortedInput, item));
        return index is 0 ? Optional<T>.None : sortedInput[index];
    }

    private static int GetUpperBoundIndex(int k) => k >> (int.TrailingZeroCount(~k) + 1);

    /// <summary>
    /// Finds a value in the sorted array that is less than or equal
    /// to the specified item.
    /// </summary>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <returns>The value that is less than <paramref name="item"/>; or <see cref="Optional{T}.None"/> if the lower bound doesn't exist.</returns>
    public readonly Optional<T> FindLowerBound<TItem>(TItem item)
        where TItem : ISupplier<T, int>
        => FindLowerBound(sorted.Span, item);

    /// <summary>
    /// Finds a value in the sorted array (prepared by <see cref="Prepare"/>) that is less than or equal
    /// to the specified item.
    /// </summary>
    /// <param name="sortedInput">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <returns>The value that is less than <paramref name="item"/>; or <see cref="Optional{T}.None"/> if the lower bound doesn't exist.</returns>
    public static Optional<T> FindLowerBound<TItem>(ReadOnlySpan<T> sortedInput, TItem item)
        where TItem : ISupplier<T, int>
    {
        var index = GetLowerBoundIndex(FindIndex(sortedInput, item));
        return index is 0 ? Optional<T>.None : sortedInput[index];
    }

    private static int GetLowerBoundIndex(int k) => k >> (int.TrailingZeroCount(k) + 1);

    /// <summary>
    /// Finds lower and upper bounds for the specified item.
    /// </summary>
    /// <param name="sortedInput">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <returns>The lower and upper bounds.</returns>
    public static (Optional<T> LowerBound, Optional<T> UpperBound) FindBounds<TItem>(ReadOnlySpan<T> sortedInput, TItem item)
        where TItem : ISupplier<T, int>
    {
        var k = FindIndex(sortedInput, item);

        var lowerBound = GetLowerBoundIndex(k);
        var upperBound = GetUpperBoundIndex(k);
        return (lowerBound is 0 ? Optional<T>.None : sortedInput[lowerBound], upperBound is 0 ? Optional<T>.None : sortedInput[upperBound]);
    }

    /// <summary>
    /// Finds lower and upper bounds for the specified item.
    /// </summary>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <returns>The lower and upper bounds.</returns>
    public readonly (Optional<T> LowerBound, Optional<T> UpperBound) FindBounds<TItem>(TItem item)
        where TItem : ISupplier<T, int>
        => FindBounds(sorted.Span, item);

    /// <summary>
    /// Releases the memory associated with this array.
    /// </summary>
    public void Dispose() => sorted.Dispose();
}

/// <summary>
/// Represents various extension methods to work with <see cref="EytzingerArray{T}"/> data type.
/// </summary>
public static class EytzingerArray
{
    /// <summary>
    /// Creates an array prepared for Eytzinger Binary Search from unsorted data.
    /// </summary>
    /// <param name="elements">The unsorted sequence of elements.</param>
    /// <param name="allocator">The allocator to be used to copy <paramref name="elements"/>.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>An array prepared for efficient binary search.</returns>
    public static EytzingerArray<T> CreateFromUnsortedSpan<T>(ReadOnlySpan<T> elements, MemoryAllocator<T>? allocator = null)
        where T : IComparable<T>
        => new(elements, static (x, y) => x.CompareTo(y), allocator);

    /// <summary>
    /// Finds a value in the sorted array that is less than or equal
    /// to the specified item.
    /// </summary>
    /// <param name="sortedInput">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <returns>The value that is less than <paramref name="item"/>; or <see cref="Optional{T}.None"/> if the lower bound doesn't exist.</returns>
    public static Optional<T> FindLowerBound<T>(ReadOnlySpan<T> sortedInput, T item)
        where T : IComparable<T>
        => EytzingerArray<T>.FindLowerBound(sortedInput, new ComparableItem<T>(item));

    /// <summary>
    /// Finds a value in the sorted array that is less than or equal
    /// to the specified item.
    /// </summary>
    /// <param name="array">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <returns>The value that is less than <paramref name="item"/>; or <see cref="Optional{T}.None"/> if the lower bound doesn't exist.</returns>
    public static Optional<T> FindLowerBound<T>(this in EytzingerArray<T> array, T item)
        where T : IComparable<T>
        => array.FindLowerBound(new ComparableItem<T>(item));
    
    /// <summary>
    /// Finds a value in the sorted array that is greater than or equal
    /// to the specified item.
    /// </summary>
    /// <param name="sortedInput">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <returns>The value that is greater than <paramref name="item"/>; or <see cref="Optional{T}.None"/> if the upper bound doesn't exist.</returns>
    public static Optional<T> FindUpperBound<T>(ReadOnlySpan<T> sortedInput, T item)
        where T : IComparable<T>
        => EytzingerArray<T>.FindUpperBound(sortedInput, new ComparableItem<T>(item));

    /// <summary>
    /// Finds a value in the sorted array that is greater than or equal
    /// to the specified item.
    /// </summary>
    /// <param name="array">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <returns>The value that is greater than <paramref name="item"/>; or <see cref="Optional{T}.None"/> if the upper bound doesn't exist.</returns>
    public static Optional<T> FindUpperBound<T>(this in EytzingerArray<T> array, T item)
        where T : IComparable<T>
        => array.FindUpperBound(new ComparableItem<T>(item));

    /// <summary>
    /// Finds lower and upper bounds for the specified item.
    /// </summary>
    /// <param name="sortedInput">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <returns>The lower and upper bounds.</returns>
    public static (Optional<T> LowerBound, Optional<T> UpperBound) FindBounds<T>(ReadOnlySpan<T> sortedInput, T item)
        where T : IComparable<T>
        => EytzingerArray<T>.FindBounds(sortedInput, new ComparableItem<T>(item));

    /// <summary>
    /// Finds lower and upper bounds for the specified item.
    /// </summary>
    /// <param name="array">The sorted array.</param>
    /// <param name="item">The value to compare the elements to.</param>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <returns>The lower and upper bounds.</returns>
    public static (Optional<T> LowerBound, Optional<T> UpperBound) FindBounds<T>(this EytzingerArray<T> array, T item)
        where T : IComparable<T>
        => array.FindBounds(new ComparableItem<T>(item));
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct ComparableItem<T>(T item) : ISupplier<T, int>
        where T : IComparable<T>
    {
        int ISupplier<T, int>.Invoke(T other) => item.CompareTo(other);

        public override string? ToString() => item.ToString();
    }
}