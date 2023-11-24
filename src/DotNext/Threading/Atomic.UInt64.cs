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
    [CLSCompliant(false)]
    public static ulong AccumulateAndGet<TAccumulator>(ref ulong value, ulong x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<ulong, ulong, ulong>
        => Accumulate<ulong, TAccumulator, InterlockedOperations>(ref value, x, accumulator).NewValue;

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
    [CLSCompliant(false)]
    public static ulong AccumulateAndGet(ref ulong value, ulong x, Func<ulong, ulong, ulong> accumulator)
        => AccumulateAndGet<DelegatingSupplier<ulong, ulong, ulong>>(ref value, x, accumulator);

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
    [CLSCompliant(false)]
    public static ulong GetAndAccumulate<TAccumulator>(ref ulong value, ulong x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<ulong, ulong, ulong>
        => Accumulate<ulong, TAccumulator, InterlockedOperations>(ref value, x, accumulator).OldValue;

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
    [CLSCompliant(false)]
    public static ulong GetAndAccumulate(ref ulong value, ulong x, Func<ulong, ulong, ulong> accumulator)
        => GetAndAccumulate<DelegatingSupplier<ulong, ulong, ulong>>(ref value, x, accumulator);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong UpdateAndGet<TUpdater>(ref ulong value, TUpdater updater)
        where TUpdater : notnull, ISupplier<ulong, ulong>
        => Update<ulong, TUpdater, InterlockedOperations>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong UpdateAndGet(ref ulong value, Func<ulong, ulong> updater)
        => UpdateAndGet<DelegatingSupplier<ulong, ulong>>(ref value, updater);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong GetAndUpdate<TUpdater>(ref ulong value, TUpdater updater)
        where TUpdater : notnull, ISupplier<ulong, ulong>
        => Update<ulong, TUpdater, InterlockedOperations>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong GetAndUpdate(ref ulong value, Func<ulong, ulong> updater)
        => GetAndUpdate<DelegatingSupplier<ulong, ulong>>(ref value, updater);
}