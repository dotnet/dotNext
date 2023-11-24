using System.Runtime.CompilerServices;

namespace DotNext.Threading;

public static partial class Atomic
{
    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="TAccumulator">The type implementing accumulator.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AccumulateAndGet<TAccumulator>(ref long value, long x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<long, long, long>
        => Accumulate<long, TAccumulator, InterlockedOperations>(ref value, x, accumulator).NewValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AccumulateAndGet(ref long value, long x, Func<long, long, long> accumulator)
        => AccumulateAndGet<DelegatingSupplier<long, long, long>>(ref value, x, accumulator);

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <typeparam name="TAccumulator">The type implementing accumulator.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndAccumulate<TAccumulator>(ref long value, long x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<long, long, long>
        => Accumulate<long, TAccumulator, InterlockedOperations>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndAccumulate(ref long value, long x, Func<long, long, long> accumulator)
        => GetAndAccumulate<DelegatingSupplier<long, long, long>>(ref value, x, accumulator);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UpdateAndGet<TUpdater>(ref long value, TUpdater updater)
        where TUpdater : notnull, ISupplier<long, long>
        => Update<long, TUpdater, InterlockedOperations>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UpdateAndGet(ref long value, Func<long, long> updater)
        => UpdateAndGet<DelegatingSupplier<long, long>>(ref value, updater);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndUpdate<TUpdater>(ref long value, TUpdater updater)
        where TUpdater : notnull, ISupplier<long, long>
        => Update<long, TUpdater, InterlockedOperations>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndUpdate(ref long value, Func<long, long> updater)
        => GetAndUpdate<DelegatingSupplier<long, long>>(ref value, updater);
}