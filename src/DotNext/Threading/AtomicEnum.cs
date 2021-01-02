using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using static InlineIL.IL;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;
using Var = InlineIL.LocalVar;

namespace DotNext.Threading
{
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
            const string resultVar = "result";
            const string nonFastPath = "nonFastPath";
            DeclareLocals(false, new Var(resultVar, typeof(long)));
            Push(ref value);
            Emit.Sizeof<TEnum>();
            Emit.Ldc_I4_8();
            Emit.Beq(nonFastPath);

            // fast path - use volatile read instruction
            Emit.Volatile();
            Emit.Ldobj<TEnum>();
            Emit.Ret();

            // non-fast path - use Volatile class
            MarkLabel(nonFastPath);
            Emit.Call(Method(typeof(Volatile), nameof(Volatile.Read), Type<long>().MakeByRefType()));
            Emit.Stloc(resultVar);
            Emit.Ldloca(resultVar);
            Emit.Ldobj<TEnum>();
            return Return<TEnum>();
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
            const string nonFastPath = "nonFastPath";
            Push(ref value);
            Emit.Sizeof<TEnum>();
            Emit.Ldc_I4_8();
            Emit.Beq(nonFastPath);

            // fast path - use volatile write instruction
            Push(newValue);
            Emit.Volatile();
            Emit.Stobj<TEnum>();
            Emit.Ret();

            // non-fast path - use Volatile class
            MarkLabel(nonFastPath);
            Push(ref newValue);
            Emit.Ldind_I8();
            Emit.Call(Method(typeof(Volatile), nameof(Volatile.Write), Type<long>().MakeByRefType(), typeof(long)));
            Emit.Ret();
        }
    }

    /// <summary>
    /// Represents atomic enum value.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    [Serializable]
    [SuppressMessage("Usage", "CA2231")]
    public struct AtomicEnum<TEnum> : IEquatable<TEnum>, ISerializable
        where TEnum : struct, Enum
    {
        private const string ValueSerData = "value";

        private long value;

        /// <summary>
        /// Initializes a new atomic boolean container with initial value.
        /// </summary>
        /// <param name="value">Initial value of the atomic boolean.</param>
        public AtomicEnum(TEnum value) => this.value = value.ToInt64();

        private AtomicEnum(SerializationInfo info, StreamingContext context)
        {
            value = (long)info.GetValue(ValueSerData, typeof(long))!;
        }

        /// <summary>
        /// Gets or sets enum value in volatile manner.
        /// </summary>
        public TEnum Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => Unsafe.AsRef(in value).VolatileRead().ToEnum<TEnum>();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.value.VolatileWrite(value.ToInt64());
        }

        /// <summary>
        /// Atomically sets referenced value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="update">The new value.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TEnum CompareExchange(TEnum update, TEnum expected)
            => Interlocked.CompareExchange(ref value, update.ToInt64(), expected.ToInt64()).ToEnum<TEnum>();

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
        public TEnum GetAndSet(TEnum update) => value.GetAndSet(update.ToInt64()).ToEnum<TEnum>();

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

        private (TEnum OldValue, TEnum NewValue) Update(in ValueFunc<TEnum, TEnum> updater)
        {
            TEnum oldValue, newValue;
            do
            {
                newValue = updater.Invoke(oldValue = Volatile.Read(ref value).ToEnum<TEnum>());
            }
            while (!CompareAndSet(oldValue, newValue));
            return (oldValue, newValue);
        }

        private (TEnum OldValue, TEnum NewValue) Accumulate(TEnum x, in ValueFunc<TEnum, TEnum, TEnum> accumulator)
        {
            TEnum oldValue, newValue;
            do
            {
                newValue = accumulator.Invoke(oldValue = Volatile.Read(ref value).ToEnum<TEnum>(), x);
            }
            while (!CompareAndSet(oldValue, newValue));
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
            => AccumulateAndGet(x, new ValueFunc<TEnum, TEnum, TEnum>(accumulator));

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
        public TEnum AccumulateAndGet(TEnum x, in ValueFunc<TEnum, TEnum, TEnum> accumulator)
            => Accumulate(x, accumulator).NewValue;

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
            => GetAndAccumulate(x, new ValueFunc<TEnum, TEnum, TEnum>(accumulator));

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
        public TEnum GetAndAccumulate(TEnum x, in ValueFunc<TEnum, TEnum, TEnum> accumulator)
            => Accumulate(x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        public TEnum UpdateAndGet(Func<TEnum, TEnum> updater)
            => UpdateAndGet(new ValueFunc<TEnum, TEnum>(updater));

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        public TEnum UpdateAndGet(in ValueFunc<TEnum, TEnum> updater)
            => Update(updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        public TEnum GetAndUpdate(Func<TEnum, TEnum> updater)
            => GetAndUpdate(new ValueFunc<TEnum, TEnum>(updater));

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        public TEnum GetAndUpdate(in ValueFunc<TEnum, TEnum> updater)
            => Update(updater).OldValue;

        /// <summary>
        /// Determines whether stored value is equal to
        /// value passed as argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public readonly bool Equals(TEnum other) => Unsafe.AsRef(in value).VolatileRead() == other.ToInt64();

        /// <summary>
        /// Determines whether stored value is equal to
        /// value as the passed argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public override readonly bool Equals(object? other) => other switch
        {
            TEnum b => Equals(b),
            AtomicEnum<TEnum> b => b.value.VolatileRead() == Unsafe.AsRef(in value).VolatileRead(),
            _ => false,
        };

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored boolean value.</returns>
        public override readonly int GetHashCode() => Unsafe.AsRef(in value).VolatileRead().GetHashCode();

        /// <summary>
        /// Converts the value in this container to its textual representation.
        /// </summary>
        /// <returns>The value in this container converted to string.</returns>
        public override readonly string ToString() => Value.ToString();

        /// <inheritdoc/>
        readonly void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            => info.AddValue(ValueSerData, value);
    }
}