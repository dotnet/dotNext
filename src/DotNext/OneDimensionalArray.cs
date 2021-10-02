using System.Diagnostics.CodeAnalysis;
using static System.Runtime.CompilerServices.Unsafe;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext;

using Intrinsics = Runtime.Intrinsics;

/// <summary>
/// Provides specialized methods to work with one-dimensional array.
/// </summary>
public static class OneDimensionalArray
{
    private struct RemovalCounter<T> : IConsumer<T>
    {
        internal long Count;

        void IConsumer<T>.Invoke(T item) => Count += 1;
    }

    /// <summary>
    /// Concatenates the array with the specified span of elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <param name="left">The array to concatenate.</param>
    /// <param name="right">The tail of concatenation.</param>
    /// <param name="startIndex">The starting index in <paramref name="left"/> at which <paramref name="right"/> should be inserted.</param>
    /// <returns>The array representing all elements from <paramref name="left"/> up to <paramref name="startIndex"/> exclusively including elements from <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is less than 0 or greater than length of <paramref name="left"/> array.</exception>
    public static T[] Concat<T>(this T[] left, T[] right, long startIndex)
    {
        if (startIndex < 0 || startIndex > Intrinsics.GetLength(left))
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        var result = new T[startIndex + right.LongLength];
        Array.Copy(left, result, startIndex);
        Array.Copy(right, 0L, result, startIndex, right.Length);
        return result;
    }

    /// <summary>
    /// Indicates that array is <see langword="null"/> or empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the array.</typeparam>
    /// <param name="array">The array to check.</param>
    /// <returns><see langword="true"/>, if array is <see langword="null"/> or empty.</returns>
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this T[]? array)
        => array is null || Intrinsics.GetLength(array) == 0;

    /// <summary>
    /// Applies specific action to each array element.
    /// </summary>
    /// <remarks>
    /// This method support modification of array elements
    /// because each array element is passed by reference into action.
    /// </remarks>
    /// <typeparam name="T">Type of array elements.</typeparam>
    /// <param name="array">An array to iterate.</param>
    /// <param name="action">An action to be applied for each element.</param>
    public static void ForEach<T>(this T[] array, RefAction<T, nint> action)
    {
        for (nint i = 0; i < Intrinsics.GetLength(array); i++)
            action(ref array[i], i);
    }

    /// <summary>
    /// Applies specific action to each array element.
    /// </summary>
    /// <remarks>
    /// This method support modification of array elements
    /// because each array element is passed by reference into action.
    /// </remarks>
    /// <typeparam name="T">Type of array elements.</typeparam>
    /// <typeparam name="TArg">The type of the argument to be passed to the action.</typeparam>
    /// <param name="array">An array to iterate.</param>
    /// <param name="action">An action to be applied for each element.</param>
    /// <param name="arg">The argument to be passed to the action.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
    [CLSCompliant(false)]
    public static unsafe void ForEach<T, TArg>(this T[] array, delegate*<ref T, TArg, void> action, TArg arg)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        for (nint i = 0; i < Intrinsics.GetLength(array); i++)
            action(ref array[i], arg);
    }

    /// <summary>
    /// Insert a new element into array and return modified array.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
    /// <param name="element">The object to insert.</param>
    /// <param name="index">The zero-based index at which item should be inserted.</param>
    /// <returns>A modified array with inserted element.</returns>
    public static T[] Insert<T>(this T[] array, T element, long index)
    {
        if (index < 0 || index > array.LongLength)
            throw new ArgumentOutOfRangeException(nameof(index));

        T[] result;
        if (Intrinsics.GetLength(array) == 0)
        {
            result = new[] { element };
        }
        else
        {
            result = new T[array.LongLength + 1];
            Array.Copy(array, 0, result, 0, Math.Min(index + 1, array.LongLength));
            Array.Copy(array, index, result, index + 1, array.LongLength - index);
            result[index] = element;
        }

        return result;
    }

    /// <summary>
    /// Removes the element at the specified in the array and returns modified array.
    /// </summary>
    /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <typeparam name="T">Type of array elements.</typeparam>
    /// <returns>A modified array with removed element.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is incorrect.</exception>
    public static T[] RemoveAt<T>(this T[] array, long index)
    {
        var length = Intrinsics.GetLength(array);
        if (index < 0L || index >= length)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (length == 1)
            return Array.Empty<T>();

        var newStore = new T[length - 1];
        Array.Copy(array, 0L, newStore, 0L, index);
        Array.Copy(array, index + 1L, newStore, index, length - index - 1L);
        return newStore;
    }

    private static T[] RemoveAll<T, TPredicate, TConsumer>(T[] array, TPredicate condition, ref TConsumer callback)
        where TPredicate : struct, ISupplier<T, bool>
        where TConsumer : struct, IConsumer<T>
    {
        var length = Intrinsics.GetLength(array);
        if (length == 0)
            return array;
        nint newLength = 0;
        var tempArray = new T[length];
        foreach (var item in array)
        {
            if (condition.Invoke(item))
                callback.Invoke(item);
            else
                tempArray[newLength++] = item;
        }

        if (length - newLength == 0)
            return array;

        if (newLength == 0)
            return Array.Empty<T>();

        array = new T[newLength];
        Array.Copy(tempArray, 0L, array, 0L, newLength);
        return array;
    }

    private static T[] RemoveAll<T, TPredicate>(T[] array, TPredicate condition, out long count)
        where TPredicate : struct, ISupplier<T, bool>
    {
        var counter = new RemovalCounter<T>();
        var result = RemoveAll(array, condition, ref counter);
        count = counter.Count;
        return result;
    }

    /// <summary>
    /// Removes all the elements that match the conditions defined by the specified predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
    /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
    /// <param name="count">The number of elements removed from this list.</param>
    /// <returns>A modified array with removed elements.</returns>
    public static T[] RemoveAll<T>(this T[] array, Predicate<T> match, out long count)
        => RemoveAll<T, DelegatingPredicate<T>>(array, match, out count);

    /// <summary>
    /// Removes all the elements that match the conditions defined by the specified predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
    /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
    /// <param name="count">The number of elements removed from this list.</param>
    /// <returns>A modified array with removed elements.</returns>
    [CLSCompliant(false)]
    public static unsafe T[] RemoveAll<T>(this T[] array, delegate*<T, bool> match, out long count)
        => RemoveAll<T, Supplier<T, bool>>(array, match, out count);

    /// <summary>
    /// Removes all the elements that match the conditions defined by the specified predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
    /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
    /// <param name="callback">The delegate that is used to accept removed items.</param>
    /// <returns>A modified array with removed elements.</returns>
    public static T[] RemoveAll<T>(this T[] array, Predicate<T> match, Action<T> callback)
    {
        var action = new DelegatingConsumer<T>(callback);
        return RemoveAll(array, new DelegatingPredicate<T>(match), ref action);
    }

    /// <summary>
    /// Removes all the elements that match the conditions defined by the specified predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
    /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
    /// <param name="callback">The delegate that is used to accept removed items.</param>
    /// <returns>A modified array with removed elements.</returns>
    [CLSCompliant(false)]
    public static unsafe T[] RemoveAll<T>(this T[] array, delegate*<T, bool> match, Action<T> callback)
    {
        var action = new DelegatingConsumer<T>(callback);
        return RemoveAll(array, new Supplier<T, bool>(match), ref action);
    }

    internal static T[] New<T>(long length) => length == 0L ? Array.Empty<T>() : new T[length];

    /// <summary>
    /// Removes the specified number of elements from the beginning of the array.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    /// <param name="input">Source array.</param>
    /// <param name="count">A number of elements to be removed.</param>
    /// <returns>Modified array.</returns>
    public static T[] RemoveFirst<T>(this T[] input, long count)
    {
        if (count == 0L)
            return input;

        var length = Intrinsics.GetLength(input);
        if (count >= length)
            return Array.Empty<T>();

        var result = new T[length - count];
        Array.Copy(input, count, result, 0, result.LongLength);
        return result;
    }

    /// <summary>
    /// Returns sub-array.
    /// </summary>
    /// <param name="input">Input array. Cannot be <see langword="null"/>.</param>
    /// <param name="startIndex">The index at which to begin this slice.</param>
    /// <param name="length">The desired length for the slice.</param>
    /// <typeparam name="T">Type of array elements.</typeparam>
    /// <returns>A new sliced array.</returns>
    public static T[] Slice<T>(this T[] input, long startIndex, long length)
    {
        if (startIndex >= Intrinsics.GetLength(input) || length == 0L)
            return Array.Empty<T>();

        if (startIndex == 0 && length == input.Length)
            return input;

        length = Math.Min(input.LongLength - startIndex, length);
        var result = new T[length];
        Array.Copy(input, startIndex, result, 0, length);
        return result;
    }

    /// <summary>
    /// Computes view over the specified array.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="input">The array instance.</param>
    /// <param name="range">The range in the array to return.</param>
    /// <returns>The range in <paramref name="input"/>.</returns>
    public static ArraySegment<T> Slice<T>(this T[] input, Range range)
    {
        var (start, length) = range.GetOffsetAndLength(input.Length);
        return new ArraySegment<T>(input, start, length);
    }

    /// <summary>
    /// Removes the specified number of elements from the end of the array.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    /// <param name="input">Source array.</param>
    /// <param name="count">A number of elements to be removed.</param>
    /// <returns>Modified array.</returns>
    public static T[] RemoveLast<T>(this T[] input, long count)
    {
        if (count == 0L)
            return input;

        var length = Intrinsics.GetLength(input);
        if (count >= length)
            return Array.Empty<T>();

        var result = new T[length - count];
        Array.Copy(input, result, result.LongLength);
        return result;
    }

    /// <summary>
    /// Determines whether two arrays contain the same set of bits.
    /// </summary>
    /// <remarks>
    /// This method performs bitwise equality between each pair of elements.
    /// </remarks>
    /// <typeparam name="T">Type of array elements. Should be unmanaged value type.</typeparam>
    /// <param name="first">First array for equality check.</param>
    /// <param name="second">Second array of equality check.</param>
    /// <returns><see langword="true"/>, if both arrays are equal; otherwise, <see langword="false"/>.</returns>
    public static unsafe bool BitwiseEquals<T>(this T[]? first, T[]? second)
        where T : unmanaged
    {
        if (first is null || second is null)
            return ReferenceEquals(first, second);
        if (Intrinsics.GetLength(first) != Intrinsics.GetLength(second))
            return false;
        if (Intrinsics.GetLength(first) == 0)
            return true;

        return Intrinsics.EqualsAligned(ref As<T, byte>(ref GetArrayDataReference(first)), ref As<T, byte>(ref GetArrayDataReference(second)), first.LongLength * sizeof(T));
    }

    /// <summary>
    /// Computes bitwise hash code for the array content.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="array">The array to be hashed.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>32-bit hash code of the array content.</returns>
    public static unsafe int BitwiseHashCode<T>(this T[] array, bool salted = true)
        where T : unmanaged
        => Intrinsics.GetLength(array) > 0 ? Intrinsics.GetHashCode32(ref As<T, byte>(ref GetArrayDataReference(array)), array.LongLength * sizeof(T), salted) : 0;

    private static unsafe int BitwiseHashCode<T, THashFunction>(T[] array, int hash, THashFunction hashFunction, bool salted)
        where T : unmanaged
        where THashFunction : struct, ISupplier<int, int, int>
        => Intrinsics.GetLength(array) > 0 ? Intrinsics.GetHashCode32(ref As<T, byte>(ref GetArrayDataReference(array)), array.LongLength * sizeof(T), hash, hashFunction, salted) : hash;

    /// <summary>
    /// Computes bitwise hash code for the array content using custom hash function.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="array">The array to be hashed.</param>
    /// <param name="hash">Initial value of the hash.</param>
    /// <param name="hashFunction">Custom hashing algorithm.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>32-bit hash code of the array content.</returns>
    public static int BitwiseHashCode<T>(this T[] array, int hash, Func<int, int, int> hashFunction, bool salted = true)
        where T : unmanaged
        => BitwiseHashCode<T, DelegatingSupplier<int, int, int>>(array, hash, hashFunction, salted);

    /// <summary>
    /// Computes bitwise hash code for the array content using custom hash function.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="array">The array to be hashed.</param>
    /// <param name="hash">Initial value of the hash.</param>
    /// <param name="hashFunction">Custom hashing algorithm.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>32-bit hash code of the array content.</returns>
    [CLSCompliant(false)]
    public static unsafe int BitwiseHashCode<T>(this T[] array, int hash, delegate*<int, int, int> hashFunction, bool salted = true)
        where T : unmanaged
        => BitwiseHashCode<T, Supplier<int, int, int>>(array, hash, hashFunction, salted);

    private static unsafe long BitwiseHashCode64<T, THashFunction>(T[] array, long hash, THashFunction hashFunction, bool salted)
        where T : unmanaged
        where THashFunction : struct, ISupplier<long, long, long>
        => Intrinsics.GetLength(array) > 0 ? Intrinsics.GetHashCode64(ref As<T, byte>(ref GetArrayDataReference(array)), array.LongLength * sizeof(T), hash, hashFunction, salted) : hash;

    /// <summary>
    /// Computes bitwise hash code for the array content using custom hash function.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="array">The array to be hashed.</param>
    /// <param name="hash">Initial value of the hash.</param>
    /// <param name="hashFunction">Custom hashing algorithm.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>64-bit hash code of the array content.</returns>
    public static long BitwiseHashCode64<T>(this T[] array, long hash, Func<long, long, long> hashFunction, bool salted = true)
        where T : unmanaged
        => BitwiseHashCode64<T, DelegatingSupplier<long, long, long>>(array, hash, hashFunction, salted);

    /// <summary>
    /// Computes bitwise hash code for the array content using custom hash function.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="array">The array to be hashed.</param>
    /// <param name="hash">Initial value of the hash.</param>
    /// <param name="hashFunction">Custom hashing algorithm.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>64-bit hash code of the array content.</returns>
    [CLSCompliant(false)]
    public static unsafe long BitwiseHashCode64<T>(this T[] array, long hash, delegate*<long, long, long> hashFunction, bool salted = true)
        where T : unmanaged
        => BitwiseHashCode64<T, Supplier<long, long, long>>(array, hash, hashFunction, salted);

    /// <summary>
    /// Computes bitwise hash code for the array content.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="array">The array to be hashed.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>64-bit hash code of the array content.</returns>
    public static unsafe long BitwiseHashCode64<T>(this T[] array, bool salted = true)
        where T : unmanaged
        => Intrinsics.GetLength(array) > 0 ? Intrinsics.GetHashCode64(ref As<T, byte>(ref GetArrayDataReference(array)), array.LongLength * sizeof(T), salted) : 0L;

    private sealed class ArrayEqualityComparer
    {
        private readonly object?[] first, second;

        internal ArrayEqualityComparer(object?[] first, object?[] second)
        {
            this.first = first;
            this.second = second;
        }

        internal void Iteration(long index, ParallelLoopState state)
        {
            if (!(state.ShouldExitCurrentIteration || Equals(first[index], second[index])))
                state.Break();
        }
    }

    /// <summary>
    /// Determines whether two arrays contain the same set of elements.
    /// </summary>
    /// <remarks>
    /// This method calls <see cref="object.Equals(object, object)"/> for each element type.
    /// </remarks>
    /// <param name="first">The first array to compare.</param>
    /// <param name="second">The second array to compare.</param>
    /// <param name="parallel"><see langword="true"/> to perform parallel iteration over array elements; <see langword="false"/> to perform sequential iteration.</param>
    /// <returns><see langword="true"/>, if both arrays are equal; otherwise, <see langword="false"/>.</returns>
    public static bool SequenceEqual(this object?[]? first, object?[]? second, bool parallel = false)
    {
        static bool EqualsSequential(object?[] first, object?[] second)
        {
            for (nint i = 0; i < Intrinsics.GetLength(first); i++)
            {
                if (!Equals(first[i], second[i]))
                    return false;
            }

            return true;
        }

        static bool EqualsParallel(object?[] first, object?[] second)
            => Parallel.For(0L, first.LongLength, new ArrayEqualityComparer(first, second).Iteration).IsCompleted;

        if (ReferenceEquals(first, second))
            return true;
        if (first is null)
            return second is null;
        if (second is null || Intrinsics.GetLength(first) != Intrinsics.GetLength(second))
            return false;

        return parallel ? EqualsParallel(first, second) : EqualsSequential(first, second);
    }

    /// <summary>
    /// Compares content of the two arrays.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="first">The first array to compare.</param>
    /// <param name="second">The second array to compare.</param>
    /// <returns>Comparison result.</returns>
    public static unsafe int BitwiseCompare<T>(this T[]? first, T[]? second)
        where T : unmanaged
    {
        if (first.IsNullOrEmpty())
            return second.IsNullOrEmpty() ? 0 : -1;

        if (second.IsNullOrEmpty())
            return 1;

        var cmp = first.LongLength.CompareTo(second.LongLength);
        if (cmp == 0)
            cmp = Intrinsics.Compare(ref As<T, byte>(ref GetArrayDataReference(first)), ref As<T, byte>(ref GetArrayDataReference(second)), first.LongLength * sizeof(T));

        return cmp;
    }

    /// <summary>
    /// Initializes every element with a new instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of array elements.</typeparam>
    /// <param name="array">The array to initialize.</param>
    public static void InstantiateElements<T>(this T[] array)
        where T : new()
    {
        for (nint i = 0; i < Intrinsics.GetLength(array); i++)
            Add(ref GetArrayDataReference(array), i) = new T();
    }
}