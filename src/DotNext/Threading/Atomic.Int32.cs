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
    public static int AccumulateAndGet<TAccumulator>(ref int value, int x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<int, int, int>
        => Accumulate<int, TAccumulator, InterlockedOperations>(ref value, x, accumulator).NewValue;

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
    public static int AccumulateAndGet(ref int value, int x, Func<int, int, int> accumulator)
        => AccumulateAndGet<DelegatingSupplier<int, int, int>>(ref value, x, accumulator);

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
    public static int GetAndAccumulate<TAccumulator>(ref int value, int x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<int, int, int>
        => Accumulate<int, TAccumulator, InterlockedOperations>(ref value, x, accumulator).OldValue;

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
    public static int GetAndAccumulate(ref int value, int x, Func<int, int, int> accumulator)
        => GetAndAccumulate<DelegatingSupplier<int, int, int>>(ref value, x, accumulator);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UpdateAndGet<TUpdater>(ref int value, TUpdater updater)
        where TUpdater : notnull, ISupplier<int, int>
        => Update<int, TUpdater, InterlockedOperations>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UpdateAndGet(ref int value, Func<int, int> updater)
        => UpdateAndGet<DelegatingSupplier<int, int>>(ref value, updater);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetAndUpdate<TUpdater>(ref int value, TUpdater updater)
        where TUpdater : notnull, ISupplier<int, int>
        => Update<int, TUpdater, InterlockedOperations>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetAndUpdate(ref int value, Func<int, int> updater)
        => GetAndUpdate<DelegatingSupplier<int, int>>(ref value, updater);
}