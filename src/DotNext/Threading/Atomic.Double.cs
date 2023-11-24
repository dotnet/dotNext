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
    public static double AccumulateAndGet<TAccumulator>(ref double value, double x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<double, double, double>
        => Accumulate<double, TAccumulator, InterlockedOperations>(ref value, x, accumulator).NewValue;

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
    public static double AccumulateAndGet(ref double value, double x, Func<double, double, double> accumulator)
        => AccumulateAndGet<DelegatingSupplier<double, double, double>>(ref value, x, accumulator);

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
    public static double GetAndAccumulate<TAccumulator>(ref double value, double x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<double, double, double>
        => Accumulate<double, TAccumulator, InterlockedOperations>(ref value, x, accumulator).OldValue;

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
    public static double GetAndAccumulate(ref double value, double x, Func<double, double, double> accumulator)
        => GetAndAccumulate<DelegatingSupplier<double, double, double>>(ref value, x, accumulator);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double UpdateAndGet<TUpdater>(ref double value, TUpdater updater)
        where TUpdater : notnull, ISupplier<double, double>
        => Update<double, TUpdater, InterlockedOperations>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double UpdateAndGet(ref double value, Func<double, double> updater)
        => UpdateAndGet<DelegatingSupplier<double, double>>(ref value, updater);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetAndUpdate<TUpdater>(ref double value, TUpdater updater)
        where TUpdater : notnull, ISupplier<double, double>
        => Update<double, TUpdater, InterlockedOperations>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetAndUpdate(ref double value, Func<double, double> updater)
        => GetAndUpdate<DelegatingSupplier<double, double>>(ref value, updater);
}