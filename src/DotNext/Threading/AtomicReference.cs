using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Provides atomic operations for the reference type.
/// </summary>
public static class AtomicReference
{
    /// <summary>
    /// Compares two values for equality and, if they are equal,
    /// replaces the stored value.
    /// </summary>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns>true if successful. False return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareAndSet<T>(ref T value, T expected, T update)
        where T : class?
        => ReferenceEquals(Interlocked.CompareExchange(ref value, update, expected), expected);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (T OldValue, T NewValue) Update<T, TUpdater>(ref T value, TUpdater updater)
        where T : class?
        where TUpdater : struct, ISupplier<T, T>
    {
        T oldValue, newValue;
        do
        {
            newValue = updater.Invoke(oldValue = Volatile.Read(ref value));
        }
        while (!CompareAndSet(ref value, oldValue, newValue));
        return (oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (T OldValue, T NewValue) Accumulate<T, TAccumulator>(ref T value, T x, TAccumulator accumulator)
        where T : class?
        where TAccumulator : struct, ISupplier<T, T, T>
    {
        T oldValue, newValue;
        do
        {
            newValue = accumulator.Invoke(oldValue = Volatile.Read(ref value), x);
        }
        while (!CompareAndSet(ref value, oldValue, newValue));
        return (oldValue, newValue);
    }

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AccumulateAndGet<T>(ref T value, T x, Func<T, T, T> accumulator)
        where T : class?
        => Accumulate<T, DelegatingSupplier<T, T, T>>(ref value, x, accumulator).NewValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe T AccumulateAndGet<T>(ref T value, T x, delegate*<T, T, T> accumulator)
        where T : class?
        => Accumulate<T, Supplier<T, T, T>>(ref value, x, accumulator).NewValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull("value")]
    public static T GetAndAccumulate<T>(ref T value, T x, Func<T, T, T> accumulator)
        where T : class?
        => Accumulate<T, DelegatingSupplier<T, T, T>>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull("value")]
    [CLSCompliant(false)]
    public static unsafe T GetAndAccumulate<T>(ref T value, T x, delegate*<T, T, T> accumulator)
        where T : class?
        => Accumulate<T, Supplier<T, T, T>>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T UpdateAndGet<T>(ref T value, Func<T, T> updater)
        where T : class?
        => Update<T, DelegatingSupplier<T, T>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe T UpdateAndGet<T>(ref T value, delegate*<T, T> updater)
        where T : class?
        => Update<T, Supplier<T, T>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull("value")]
    public static T GetAndUpdate<T>(ref T value, Func<T, T> updater)
        where T : class?
        => Update<T, DelegatingSupplier<T, T>>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="T">Type of value in the memory storage.</typeparam>
    /// <param name="value">The value to update.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull("value")]
    [CLSCompliant(false)]
    public static unsafe T GetAndUpdate<T>(ref T value, delegate*<T, T> updater)
        where T : class?
        => Update<T, Supplier<T, T>>(ref value, updater).OldValue;

    /// <summary>
    /// Performs volatile read of the array element.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to read from.</param>
    /// <param name="index">The array element index.</param>
    /// <returns>The array element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T VolatileRead<T>(this T[] array, long index)
        where T : class?
        => Volatile.Read(ref array[index]);

    /// <summary>
    /// Performs volatile write to the array element.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to write into.</param>
    /// <param name="index">The array element index.</param>
    /// <param name="element">The new value of the array element.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VolatileWrite<T>(this T[] array, long index, T element)
        where T : class?
        => Volatile.Write(ref array[index], element);

    /// <summary>
    /// Atomically sets array element to the given updated value if the array element == the expected value.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareAndSet<T>(this T[] array, long index, T expected, T update)
        where T : class?
        => CompareAndSet(ref array[index], expected, update);

    /// <summary>
    /// Atomically sets array element to the given updated value if the array element == the expected value.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="update">The new value.</param>
    /// <param name="comparand">The expected value.</param>
    /// <returns>The original value of the array element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T CompareExchange<T>(this T[] array, long index, T update, T comparand)
        where T : class?
        => Interlocked.CompareExchange(ref array[index], update, comparand);

    /// <summary>
    /// Modifies the array element atomically.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of array element to be modified.</param>
    /// <param name="update">A new value to be stored as array element.</param>
    /// <returns>Original array element before modification.</returns>
    public static T GetAndSet<T>(this T[] array, long index, T update)
        where T : class?
        => Interlocked.Exchange(ref array[index], update);

    /// <summary>
    /// Modifies the array element atomically.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of array element to be modified.</param>
    /// <param name="update">A new value to be stored as array element.</param>
    /// <returns>The array element after modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull("update")]
    public static T SetAndGet<T>(this T[] array, long index, T update)
        where T : class?
    {
        VolatileWrite(array, index, update);
        return update;
    }

    /// <summary>
    /// Atomically updates the array element with the results of applying the given function
    /// to the array element and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the array element as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    public static T AccumulateAndGet<T>(this T[] array, long index, T x, Func<T, T, T> accumulator)
        where T : class?
        => AccumulateAndGet(ref array[index], x, accumulator);

    /// <summary>
    /// Atomically updates the array element with the results of applying the given function
    /// to the array element and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the array element as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [CLSCompliant(false)]
    public static unsafe T AccumulateAndGet<T>(this T[] array, long index, T x, delegate*<T, T, T> accumulator)
        where T : class?
        => AccumulateAndGet(ref array[index], x, accumulator);

    /// <summary>
    /// Atomically updates the array element with the results of applying the given function
    /// to the array element and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the array element as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value of the array element.</returns>
    public static T GetAndAccumulate<T>(this T[] array, long index, T x, Func<T, T, T> accumulator)
        where T : class?
        => GetAndAccumulate(ref array[index], x, accumulator);

    /// <summary>
    /// Atomically updates the array element with the results of applying the given function
    /// to the array element and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the array element as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value of the array element.</returns>
    [CLSCompliant(false)]
    public static unsafe T GetAndAccumulate<T>(this T[] array, long index, T x, delegate*<T, T, T> accumulator)
        where T : class?
        => GetAndAccumulate(ref array[index], x, accumulator);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    public static T UpdateAndGet<T>(this T[] array, long index, Func<T, T> updater)
        where T : class?
        => UpdateAndGet(ref array[index], updater);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [CLSCompliant(false)]
    public static unsafe T UpdateAndGet<T>(this T[] array, long index, delegate*<T, T> updater)
        where T : class?
        => UpdateAndGet(ref array[index], updater);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="T">The type of the elements in array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value of the array element.</returns>
    public static T GetAndUpdate<T>(this T[] array, long index, Func<T, T> updater)
        where T : class?
        => GetAndUpdate(ref array[index], updater);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value of the array element.</returns>
    [CLSCompliant(false)]
    public static unsafe T GetAndUpdate<T>(this T[] array, long index, delegate*<T, T> updater)
        where T : class?
        => GetAndUpdate(ref array[index], updater);
}