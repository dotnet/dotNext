using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static InlineIL.IL;

namespace DotNext.Threading;

using static Runtime.Intrinsics;

/// <summary>
/// Provides basic atomic operations for arbitrary enum type.
/// </summary>
public static class AtomicEnum
{
    /// <summary>
    /// Reads the value of the specified field. On systems that require it, inserts a
    /// memory barrier that prevents the processor from reordering memory operations
    /// as follows: If a read or write appears after this method in the code, the processor
    /// cannot move it before this method.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The field to read.</param>
    /// <returns>
    /// The value that was read. This value is the latest written by any processor in
    /// the computer, regardless of the number of processors or the state of processor
    /// cache.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEnum VolatileRead<TEnum>(this ref TEnum value)
        where TEnum : struct, Enum
    {
        return Unsafe.SizeOf<TEnum>() == sizeof(long) && IntPtr.Size != sizeof(long)
            ? ReinterpretCast<long, TEnum>(Volatile.Read(ref Unsafe.As<TEnum, long>(ref value)))
            : ReadCore(ref value);

        static TEnum ReadCore(ref TEnum location)
        {
            PushInRef(in location);
            Emit.Volatile();
            Emit.Ldobj<TEnum>();
            return Return<TEnum>();
        }
    }

    /// <summary>
    /// Writes the specified value to the specified field. On systems that require it,
    /// inserts a memory barrier that prevents the processor from reordering memory operations
    /// as follows: If a read or write appears before this method in the code, the processor
    /// cannot move it after this method.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The field where the value is written.</param>
    /// <param name="newValue">
    /// The value to write. The value is written immediately so that it is visible to
    /// all processors in the computer.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VolatileWrite<TEnum>(this ref TEnum value, TEnum newValue)
        where TEnum : struct, Enum
    {
        if (Unsafe.SizeOf<TEnum>() == sizeof(long) && IntPtr.Size != sizeof(long))
            Volatile.Write(ref Unsafe.As<TEnum, long>(ref value), ReinterpretCast<TEnum, long>(newValue));
        else
            WriteCore(ref value, newValue);

        static void WriteCore(ref TEnum location, TEnum value)
        {
            Push(ref location);
            Push(value);
            Emit.Volatile();
            Emit.Stobj<TEnum>();
            Emit.Ret();
        }
    }
}

/// <summary>
/// Represents atomic enum value.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
[SuppressMessage("Usage", "CA2231")]
public struct AtomicEnum<TEnum> : IEquatable<TEnum>
    where TEnum : struct, Enum
{
    private ulong value;

    /// <summary>
    /// Initializes a new atomic boolean container with initial value.
    /// </summary>
    /// <param name="value">Initial value of the atomic boolean.</param>
    public AtomicEnum(TEnum value) => this.value = value.ToUInt64Unchecked();

    /// <summary>
    /// Gets or sets enum value in volatile manner.
    /// </summary>
    public TEnum Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => value.VolatileRead().ToEnumUnchecked<TEnum>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.value.VolatileWrite(value.ToUInt64Unchecked());
    }

    /// <summary>
    /// Atomically sets referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="update">The new value.</param>
    /// <param name="expected">The expected value.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEnum CompareExchange(TEnum update, TEnum expected)
        => Interlocked.CompareExchange(ref value, update.ToUInt64Unchecked(), expected.ToUInt64Unchecked()).ToEnumUnchecked<TEnum>();

    /// <summary>
    /// Atomically sets referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareAndSet(TEnum expected, TEnum update) => EqualityComparer<TEnum>.Default.Equals(CompareExchange(update, expected), expected);

    /// <summary>
    /// Modifies the current value atomically.
    /// </summary>
    /// <param name="update">A new value to be stored into this container.</param>
    /// <returns>Original value before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEnum GetAndSet(TEnum update) => value.GetAndSet(update.ToUInt64Unchecked()).ToEnumUnchecked<TEnum>();

    /// <summary>
    /// Modifies the current value atomically.
    /// </summary>
    /// <param name="update">A new value to be stored into this container.</param>
    /// <returns>A new value passed as argument.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEnum SetAndGet(TEnum update)
    {
        Value = update;
        return update;
    }

    private (TEnum OldValue, TEnum NewValue) Update<TUpdater>(TUpdater updater)
        where TUpdater : struct, ISupplier<TEnum, TEnum>
    {
        TEnum oldValue, newValue, tmp = Value;
        do
        {
            newValue = updater.Invoke(oldValue = tmp);
        }
        while (!EqualityComparer<TEnum>.Default.Equals(tmp = CompareExchange(newValue, oldValue), oldValue));

        return (oldValue, newValue);
    }

    private (TEnum OldValue, TEnum NewValue) Accumulate<TAccumulator>(TEnum x, TAccumulator accumulator)
        where TAccumulator : struct, ISupplier<TEnum, TEnum, TEnum>
    {
        TEnum oldValue, newValue, tmp = Value;
        do
        {
            newValue = accumulator.Invoke(oldValue = tmp, x);
        }
        while (!EqualityComparer<TEnum>.Default.Equals(tmp = CompareExchange(newValue, oldValue), oldValue));

        return (oldValue, newValue);
    }

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    public TEnum AccumulateAndGet(TEnum x, Func<TEnum, TEnum, TEnum> accumulator)
        => Accumulate<DelegatingSupplier<TEnum, TEnum, TEnum>>(x, accumulator).NewValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [CLSCompliant(false)]
    public unsafe TEnum AccumulateAndGet(TEnum x, delegate*<TEnum, TEnum, TEnum> accumulator)
        => Accumulate<Supplier<TEnum, TEnum, TEnum>>(x, accumulator).NewValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    public TEnum GetAndAccumulate(TEnum x, Func<TEnum, TEnum, TEnum> accumulator)
        => Accumulate<DelegatingSupplier<TEnum, TEnum, TEnum>>(x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [CLSCompliant(false)]
    public unsafe TEnum GetAndAccumulate(TEnum x, delegate*<TEnum, TEnum, TEnum> accumulator)
        => Accumulate<Supplier<TEnum, TEnum, TEnum>>(x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    public TEnum UpdateAndGet(Func<TEnum, TEnum> updater)
        => Update<DelegatingSupplier<TEnum, TEnum>>(updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [CLSCompliant(false)]
    public unsafe TEnum UpdateAndGet(delegate*<TEnum, TEnum> updater)
        => Update<Supplier<TEnum, TEnum>>(updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    public TEnum GetAndUpdate(Func<TEnum, TEnum> updater)
        => Update<DelegatingSupplier<TEnum, TEnum>>(updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [CLSCompliant(false)]
    public unsafe TEnum GetAndUpdate(delegate*<TEnum, TEnum> updater)
        => Update<Supplier<TEnum, TEnum>>(updater).OldValue;

    /// <summary>
    /// Determines whether stored value is equal to
    /// value passed as argument.
    /// </summary>
    /// <param name="other">Other value to compare.</param>
    /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
    public readonly bool Equals(TEnum other) => EqualityComparer<TEnum>.Default.Equals(Value, other);

    /// <summary>
    /// Determines whether stored value is equal to
    /// value as the passed argument.
    /// </summary>
    /// <param name="other">Other value to compare.</param>
    /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
    public override readonly bool Equals([NotNullWhen(true)] object? other) => other switch
    {
        TEnum b => Equals(b),
        AtomicEnum<TEnum> b => Equals(b.Value),
        _ => false,
    };

    /// <summary>
    /// Computes hash code for the stored value.
    /// </summary>
    /// <returns>The hash code of the stored boolean value.</returns>
    public override readonly int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Converts the value in this container to its textual representation.
    /// </summary>
    /// <returns>The value in this container converted to string.</returns>
    public override readonly string ToString() => Value.ToString();
}