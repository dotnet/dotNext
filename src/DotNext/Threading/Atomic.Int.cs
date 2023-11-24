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
    public static nint AccumulateAndGet<TAccumulator>(ref nint value, nint x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<nint, nint, nint>
        => Accumulate<nint, TAccumulator, InterlockedOperations>(ref value, x, accumulator).NewValue;

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
    public static nint AccumulateAndGet(ref nint value, nint x, Func<nint, nint, nint> accumulator)
        => AccumulateAndGet<DelegatingSupplier<nint, nint, nint>>(ref value, x, accumulator);

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
    public static nint GetAndAccumulate<TAccumulator>(ref nint value, nint x, TAccumulator accumulator)
        where TAccumulator : notnull, ISupplier<nint, nint, nint>
        => Accumulate<nint, TAccumulator, InterlockedOperations>(ref value, x, accumulator).OldValue;

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
    public static nint GetAndAccumulate(ref nint value, nint x, Func<nint, nint, nint> accumulator)
        => GetAndAccumulate<DelegatingSupplier<nint, nint, nint>>(ref value, x, accumulator);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint UpdateAndGet<TUpdater>(ref nint value, TUpdater updater)
        where TUpdater : notnull, ISupplier<nint, nint>
        => Update<nint, TUpdater, InterlockedOperations>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint UpdateAndGet(ref nint value, Func<nint, nint> updater)
        => UpdateAndGet<DelegatingSupplier<nint, nint>>(ref value, updater);

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint GetAndUpdate<TUpdater>(ref nint value, TUpdater updater)
        where TUpdater : notnull, ISupplier<nint, nint>
        => Update<nint, TUpdater, InterlockedOperations>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint GetAndUpdate(ref nint value, Func<nint, nint> updater)
        => GetAndUpdate<DelegatingSupplier<nint, nint>>(ref value, updater);
}