using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.FieldRef;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Threading;

/// <summary>
/// Represents atomic boolean.
/// </summary>
[SuppressMessage("Usage", "CA2231")]
public struct AtomicBoolean : IEquatable<bool>
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct Negation : ISupplier<bool, bool>
    {
        bool ISupplier<bool, bool>.Invoke(bool value) => !value;
    }

    private int value;

    /// <summary>
    /// Initializes a new atomic boolean container with initial value.
    /// </summary>
    /// <param name="value">Initial value of the atomic boolean.</param>
    public AtomicBoolean(bool value) => this.value = value.ToInt32();

    /// <summary>
    /// Gets or sets boolean value in volatile manner.
    /// </summary>
    public bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            Ldarg_0();
            Volatile();
            Ldfld(Field(Type<AtomicBoolean>(), nameof(value)));
            return Return<bool>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            Ldarg_0();
            Push(value);
            Volatile();
            Stfld(Field(Type<AtomicBoolean>(), nameof(this.value)));
            Ret();
        }
    }

    /// <summary>
    /// Atomically sets referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="update">The new value.</param>
    /// <param name="expected">The expected value.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareExchange(bool update, bool expected)
    {
        // push this.value
        Ldarg_0();
        Ldflda(Field(Type<AtomicBoolean>(), nameof(value)));

        Push(update);
        Push(expected);

        Call(Method(Type(typeof(Interlocked)), nameof(Interlocked.CompareExchange), Type<int>().MakeByRefType(), Type<int>(), Type<int>()));
        return Return<bool>();
    }

    /// <summary>
    /// Atomically sets referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareAndSet(bool expected, bool update)
        => CompareExchange(update, expected) == expected;

    /// <summary>
    /// Atomically sets <see langword="true"/> value if the
    /// current value is <see langword="false"/>.
    /// </summary>
    /// <returns><see langword="true"/> if current value is modified successfully; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool FalseToTrue() => CompareAndSet(false, true);

    /// <summary>
    /// Atomically sets <see langword="false"/> value if the
    /// current value is <see langword="true"/>.
    /// </summary>
    /// <returns><see langword="true"/> if current value is modified successfully; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrueToFalse() => CompareAndSet(true, false);

    /// <summary>
    /// Negates currently stored value atomically.
    /// </summary>
    /// <returns>Negation result.</returns>
    public unsafe bool NegateAndGet() => Update(new Negation()).NewValue;

    /// <summary>
    /// Negates currently stored value atomically.
    /// </summary>
    /// <returns>The original value before negation.</returns>
    public unsafe bool GetAndNegate() => Update(new Negation()).OldValue;

    /// <summary>
    /// Modifies the current value atomically.
    /// </summary>
    /// <param name="update">A new value to be stored into this container.</param>
    /// <returns>Original value before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetAndSet(bool update) => value.GetAndSet(update.ToInt32()).ToBoolean();

    /// <summary>
    /// Modifies the current value atomically.
    /// </summary>
    /// <param name="update">A new value to be stored into this container.</param>
    /// <returns>A new value passed as argument.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetAndGet(bool update)
    {
        Value = update;
        return update;
    }

    private (bool OldValue, bool NewValue) Update<TUpdater>(TUpdater updater)
        where TUpdater : struct, ISupplier<bool, bool>
    {
        bool oldValue, newValue, tmp = Value;
        do
        {
            newValue = updater.Invoke(oldValue = tmp);
        }
        while ((tmp = CompareExchange(newValue, oldValue)) != oldValue);

        return (oldValue, newValue);
    }

    private (bool OldValue, bool NewValue) Accumulate<TAccumulator>(bool x, TAccumulator accumulator)
        where TAccumulator : struct, ISupplier<bool, bool, bool>
    {
        bool oldValue, newValue, tmp = Value;
        do
        {
            newValue = accumulator.Invoke(oldValue = tmp, x);
        }
        while ((tmp = CompareExchange(newValue, oldValue)) != oldValue);

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
    public bool AccumulateAndGet(bool x, Func<bool, bool, bool> accumulator)
        => Accumulate<DelegatingSupplier<bool, bool, bool>>(x, accumulator).NewValue;

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
    public unsafe bool AccumulateAndGet(bool x, delegate*<bool, bool, bool> accumulator)
        => Accumulate<Supplier<bool, bool, bool>>(x, accumulator).NewValue;

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
    public bool GetAndAccumulate(bool x, Func<bool, bool, bool> accumulator)
        => Accumulate<DelegatingSupplier<bool, bool, bool>>(x, accumulator).OldValue;

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
    public unsafe bool GetAndAccumulate(bool x, delegate*<bool, bool, bool> accumulator)
        => Accumulate<Supplier<bool, bool, bool>>(x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    public bool UpdateAndGet(Func<bool, bool> updater)
        => Update<DelegatingSupplier<bool, bool>>(updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [CLSCompliant(false)]
    public unsafe bool UpdateAndGet(delegate*<bool, bool> updater)
        => Update<Supplier<bool, bool>>(updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    public bool GetAndUpdate(Func<bool, bool> updater)
        => Update<DelegatingSupplier<bool, bool>>(updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [CLSCompliant(false)]
    public unsafe bool GetAndUpdate(delegate*<bool, bool> updater)
        => Update<Supplier<bool, bool>>(updater).OldValue;

    internal void Acquire()
    {
        ref var lockState = ref value;
        if (Interlocked.Exchange(ref lockState, 1) is 1)
            Contention(ref lockState);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Contention(ref int value)
        {
            var spinner = new SpinWait();
            do
            {
                spinner.SpinOnce();
            }
            while (Interlocked.Exchange(ref value, 1) is 1);
        }
    }

    internal void Release() => System.Threading.Volatile.Write(ref value, 0);

    /// <summary>
    /// Determines whether stored value is equal to value passed as argument.
    /// </summary>
    /// <param name="other">Other value to compare.</param>
    /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
    public readonly bool Equals(bool other) => Unsafe.AsRef(in value).VolatileRead() == other.ToInt32();

    /// <summary>
    /// Computes hash code for the stored value.
    /// </summary>
    /// <returns>The hash code of the stored boolean value.</returns>
    public override readonly int GetHashCode() => Unsafe.AsRef(in value).VolatileRead();

    /// <summary>
    /// Determines whether stored value is equal to
    /// value as the passed argument.
    /// </summary>
    /// <param name="other">Other value to compare.</param>
    /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
    public override readonly bool Equals([NotNullWhen(true)] object? other) => other switch
    {
        bool b => Equals(b),
        AtomicBoolean b => Value == b.Value,
        _ => false,
    };

    /// <summary>
    /// Returns stored boolean value in the form of <see cref="string"/>.
    /// </summary>
    /// <returns>Textual representation of stored boolean value.</returns>
    public override readonly string ToString() => value.ToBoolean() ? bool.TrueString : bool.FalseString;
}