using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using static InlineIL.IL;
using MR = InlineIL.MethodRef;
using TR = InlineIL.TypeRef;
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
        /// <param name="value">The field to read.</param>
        /// <returns>
        /// The value that was read. This value is the latest written by any processor in
        /// the computer, regardless of the number of processors or the state of processor
        /// cache.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static E VolatileRead<E>(this ref E value)
            where E : struct, Enum
        {
            const string resultVar = "result";
            const string nonFastPath = "nonFastPath";
            DeclareLocals(false, new Var(resultVar, typeof(long)));
            Push(ref value);
            Emit.Sizeof(typeof(E));
            Emit.Ldc_I4_8();
            Emit.Beq(nonFastPath);
            //fast path - use volatile read instruction
            Emit.Volatile();
            Emit.Ldobj(typeof(E));
            Emit.Ret();
            //non-fast path - use Volatile class
            MarkLabel(nonFastPath);
            Emit.Call(new MR(typeof(Volatile), nameof(Volatile.Read), new TR(typeof(long)).MakeByRefType()));
            Emit.Stloc(resultVar);
            Emit.Ldloca(resultVar);
            Emit.Ldobj(typeof(E));
            return Return<E>();
        }

        /// <summary>
        /// Writes the specified value to the specified field. On systems that require it,
        /// inserts a memory barrier that prevents the processor from reordering memory operations
        /// as follows: If a read or write appears before this method in the code, the processor
        /// cannot move it after this method.
        /// </summary>
        /// <param name="value">The field where the value is written.</param>
        /// <param name="newValue">
        /// The value to write. The value is written immediately so that it is visible to
        /// all processors in the computer.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VolatileWrite<E>(this ref E value, E newValue)
            where E : struct, Enum
        {
            const string nonFastPath = "nonFastPath";
            Push(ref value);
            Emit.Sizeof(typeof(E));
            Emit.Ldc_I4_8();
            Emit.Beq(nonFastPath);
            //fast path - use volatile write instruction
            Push(newValue);
            Emit.Volatile();
            Emit.Stobj(typeof(E));
            Emit.Ret();
            //non-fast path - use Volatile class
            MarkLabel(nonFastPath);
            Push(ref newValue);
            Emit.Ldind_I8();
            Emit.Call(new MR(typeof(Volatile), nameof(Volatile.Write), new TR(typeof(long)).MakeByRefType(), typeof(long)));
            Emit.Ret();
        }
    }

    /// <summary>
    /// Represents atomic enum value.
    /// </summary>
    [Serializable]
    [SuppressMessage("Design", "CA1066")]
    [SuppressMessage("Usage", "CA2231")]
    public struct AtomicEnum<E> : IEquatable<E>, ISerializable
        where E : struct, Enum
    {
        private const string ValueSerData = "value";

        private long value;

        /// <summary>
        /// Initializes a new atomic boolean container with initial value.
        /// </summary>
        /// <param name="value">Initial value of the atomic boolean.</param>
        public AtomicEnum(E value) => this.value = value.ToInt64();

        [SuppressMessage("Usage", "CA1801", Justification = "context is required by .NET serialization framework")]
        private AtomicEnum(SerializationInfo info, StreamingContext context)
        {
            value = (long)info.GetValue(ValueSerData, typeof(long));
        }

        /// <summary>
        /// Gets or sets enum value in volatile manner.
        /// </summary>
        public E Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value.VolatileRead().ToEnum<E>();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.value.VolatileWrite(value.ToInt64());
        }

        /// <summary>
		/// Atomically sets referenced value to the given updated value if the current value == the expected value.
		/// </summary>
		/// <param name="expected">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns>The original value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E CompareExchange(E update, E expected)
            => Interlocked.CompareExchange(ref value, update.ToInt64(), expected.ToInt64()).ToEnum<E>();

        /// <summary>
        /// Atomically sets referenced value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareAndSet(E expected, E update) => EqualityComparer<E>.Default.Equals(CompareExchange(update, expected), expected);

        /// <summary>
		/// Modifies the current value atomically.
		/// </summary>
		/// <param name="update">A new value to be stored into this container.</param>
		/// <returns>Original value before modification.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E GetAndSet(E update) => value.GetAndSet(update.ToInt64()).ToEnum<E>();

        /// <summary>
		/// Modifies the current value atomically.
		/// </summary>
		/// <param name="update">A new value to be stored into this container.</param>
		/// <returns>A new value passed as argument.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E SetAndGet(E update)
        {
            Value = update;
            return update;
        }

        private (E OldValue, E NewValue) Update(in ValueFunc<E, E> updater)
        {
            E oldValue, newValue;
            do
            {
                newValue = updater.Invoke(oldValue = Volatile.Read(ref value).ToEnum<E>());
            }
            while (!CompareAndSet(oldValue, newValue));
            return (oldValue, newValue);
        }

        private (E OldValue, E NewValue) Accumulate(E x, in ValueFunc<E, E, E> accumulator)
        {
            E oldValue, newValue;
            do
            {
                newValue = accumulator.Invoke(oldValue = Volatile.Read(ref value).ToEnum<E>(), x);
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
		/// <param name="accumulator">A side-effect-free function of two arguments</param>
		/// <returns>The updated value.</returns>
		public E AccumulateAndGet(E x, Func<E, E, E> accumulator)
            => AccumulateAndGet(x, new ValueFunc<E, E, E>(accumulator, true));

        /// <summary>
		/// Atomically updates the current value with the results of applying the given function 
		/// to the current and given values, returning the updated value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the current value as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments</param>
		/// <returns>The updated value.</returns>
		public E AccumulateAndGet(E x, in ValueFunc<E, E, E> accumulator)
            => Accumulate(x, accumulator).NewValue;

        /// <summary>
		/// Atomically updates the current value with the results of applying the given function 
		/// to the current and given values, returning the original value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the current value as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments</param>
		/// <returns>The original value.</returns>
		public E GetAndAccumulate(E x, Func<E, E, E> accumulator)
            => GetAndAccumulate(x, new ValueFunc<E, E, E>(accumulator, true));

        /// <summary>
		/// Atomically updates the current value with the results of applying the given function 
		/// to the current and given values, returning the original value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the current value as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments</param>
		/// <returns>The original value.</returns>
		public E GetAndAccumulate(E x, in ValueFunc<E, E, E> accumulator)
            => Accumulate(x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        public E UpdateAndGet(Func<E, E> updater)
            => UpdateAndGet(new ValueFunc<E, E>(updater, true));

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        public E UpdateAndGet(in ValueFunc<E, E> updater)
            => Update(updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        public E GetAndUpdate(Func<E, E> updater)
            => GetAndUpdate(new ValueFunc<E, E>(updater, true));

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        public E GetAndUpdate(in ValueFunc<E, E> updater)
            => Update(updater).OldValue;

        /// <summary>
        /// Determines whether stored value is equal to
        /// value passed as argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public bool Equals(E other) => value.VolatileRead() == other.ToInt64();

        /// <summary>
        /// Determines whether stored value is equal to
        /// value as the passed argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
        {
            switch (other)
            {
                case E b:
                    return Equals(b);
                case AtomicEnum<E> b:
                    return b.value.VolatileRead() == value.VolatileRead();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored boolean value.</returns>
        public override int GetHashCode() => value.VolatileRead().GetHashCode();

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            => info.AddValue(ValueSerData, value);
    }
}