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

    private static (T OldValue, T NewValue) Update<T, TUpdater>(ref T value, TUpdater updater)
        where T : class?
        where TUpdater : struct, ISupplier<T, T>
    {
        T oldValue, newValue, tmp = Volatile.Read(ref value);
        do
        {
            newValue = updater.Invoke(oldValue = tmp);
        }
        while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref value, newValue, oldValue), oldValue));

        return (oldValue, newValue);
    }

    private static (T OldValue, T NewValue) Accumulate<T, TAccumulator>(ref T value, T x, TAccumulator accumulator)
        where T : class?
        where TAccumulator : struct, ISupplier<T, T, T>
    {
        T oldValue, newValue, tmp = Volatile.Read(ref value);
        do
        {
            newValue = accumulator.Invoke(oldValue = tmp, x);
        }
        while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref value, newValue, oldValue), oldValue));

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
    [return: NotNullIfNotNull(nameof(value))]
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
    [return: NotNullIfNotNull(nameof(value))]
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
    [return: NotNullIfNotNull(nameof(value))]
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
    [return: NotNullIfNotNull(nameof(value))]
    [CLSCompliant(false)]
    public static unsafe T GetAndUpdate<T>(ref T value, delegate*<T, T> updater)
        where T : class?
        => Update<T, Supplier<T, T>>(ref value, updater).OldValue;
}