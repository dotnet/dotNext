using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;

namespace DotNext.Threading
{
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
            where T : class
            => ReferenceEquals(Interlocked.CompareExchange(ref value, update, expected), expected);

        private static (T OldValue, T NewValue) Update<T>(ref T value, in ValueFunc<T, T> updater)
            where T : class
        {
            T oldValue, newValue;
            do
            {
                newValue = updater.Invoke(oldValue = Volatile.Read(ref value));
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
            return (oldValue, newValue);
        }

        private static (T OldValue, T NewValue) Accumulate<T>(ref T value, T x, in ValueFunc<T, T, T> accumulator)
            where T : class
        {
            T oldValue, newValue;
            do
            {
                newValue = accumulator.Invoke(oldValue = Volatile.Read(ref value), x);
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
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
        /// <param name="accumulator">A side-effect-free function of two arguments</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AccumulateAndGet<T>(ref T value, T x, Func<T, T, T> accumulator)
            where T : class
            => AccumulateAndGet(ref value, x, new ValueFunc<T, T, T>(accumulator, true));

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
        /// <param name="accumulator">A side-effect-free function of two arguments</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AccumulateAndGet<T>(ref T value, T x, in ValueFunc<T, T, T> accumulator)
            where T : class
            => Accumulate(ref value, x, accumulator).NewValue;

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
        /// <param name="accumulator">A side-effect-free function of two arguments</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetAndAccumulate<T>(ref T value, T x, Func<T, T, T> accumulator)
            where T : class
            => GetAndAccumulate(ref value, x, new ValueFunc<T, T, T>(accumulator, true));

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
        /// <param name="accumulator">A side-effect-free function of two arguments</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetAndAccumulate<T>(ref T value, T x, in ValueFunc<T, T, T> accumulator)
            where T : class
            => Accumulate(ref value, x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <typeparam name="T">Type of value in the memory storage.</typeparam>
        /// <param name="value">The value to update.</param>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T UpdateAndGet<T>(ref T value, Func<T, T> updater)
            where T : class
            => UpdateAndGet(ref value, new ValueFunc<T, T>(updater, true));

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <typeparam name="T">Type of value in the memory storage.</typeparam>
        /// <param name="value">The value to update.</param>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T UpdateAndGet<T>(ref T value, in ValueFunc<T, T> updater)
            where T : class
            => Update(ref value, updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <typeparam name="T">Type of value in the memory storage.</typeparam>
        /// <param name="value">The value to update.</param>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetAndUpdate<T>(ref T value, Func<T, T> updater)
            where T : class
            => GetAndUpdate(ref value, new ValueFunc<T, T>(updater, true));

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <typeparam name="T">Type of value in the memory storage.</typeparam>
        /// <param name="value">The value to update.</param>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetAndUpdate<T>(ref T value, in ValueFunc<T, T> updater)
            where T : class
            => Update(ref value, updater).OldValue;

        /// <summary>
        /// Performs volatile read of the array element.
        /// </summary>
        /// <param name="array">The array to read from.</param>
        /// <param name="index">The array element index.</param>
        /// <returns>The array element.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T VolatileRead<T>(this T[] array, long index)
            where T : class
            => Volatile.Read(ref array[index]);

        /// <summary>
        /// Performs volatile write to the array element.
        /// </summary>
        /// <param name="array">The array to write into.</param>
        /// <param name="index">The array element index.</param>
        /// <param name="element">The new value of the array element.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VolatileWrite<T>(this T[] array, long index, T element)
            where T : class
            => Volatile.Write(ref array[index], element);

        /// <summary>
		/// Atomically sets array element to the given updated value if the array element == the expected value.
		/// </summary>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="expected">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareAndSet<T>(this T[] array, long index, T expected, T update)
            where T : class
            => CompareAndSet(ref array[index], expected, update);

        /// <summary>
		/// Atomically sets array element to the given updated value if the array element == the expected value.
		/// </summary>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="comparand">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns>The original value of the array element.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CompareExchange<T>(this T[] array, long index, T update, T comparand)
            where T : class
            => Interlocked.CompareExchange(ref array[index], update, comparand);

        /// <summary>
		/// Modifies the array element atomically.
		/// </summary>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of array element to be modified.</param>
		/// <param name="update">A new value to be stored as array element.</param>
		/// <returns>Original array element before modification.</returns>
		public static T GetAndSet<T>(this T[] array, long index, T update)
            where T : class
            => Interlocked.Exchange(ref array[index], update);

        /// <summary>
		/// Modifies the array element atomically.
		/// </summary>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of array element to be modified.</param>
		/// <param name="update">A new value to be stored as array element.</param>
		/// <returns>The array element after modification.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T SetAndGet<T>(this T[] array, long index, T update)
            where T : class
        {
            VolatileWrite(array, index, update);
            return update;
        }

        /// <summary>
		/// Atomically updates the array element with the results of applying the given function 
		/// to the array element and given values, returning the updated value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the array element as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments.</param>
		/// <returns>The updated value.</returns>
		public static T AccumulateAndGet<T>(this T[] array, long index, T x, Func<T, T, T> accumulator)
            where T : class
            => AccumulateAndGet(array, index, x, new ValueFunc<T, T, T>(accumulator, true));

        /// <summary>
		/// Atomically updates the array element with the results of applying the given function 
		/// to the array element and given values, returning the updated value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the array element as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments.</param>
		/// <returns>The updated value.</returns>
		public static T AccumulateAndGet<T>(this T[] array, long index, T x, in ValueFunc<T, T, T> accumulator)
            where T : class
            => AccumulateAndGet(ref array[index], x, accumulator);

        /// <summary>
		/// Atomically updates the array element with the results of applying the given function 
		/// to the array element and given values, returning the original value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the array element as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments.</param>
		/// <returns>The original value of the array element.</returns>
		public static T GetAndAccumulate<T>(this T[] array, long index, T x, Func<T, T, T> accumulator)
            where T : class
            => GetAndAccumulate(array, index, x, new ValueFunc<T, T, T>(accumulator, true));

        /// <summary>
		/// Atomically updates the array element with the results of applying the given function 
		/// to the array element and given values, returning the original value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the array element as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments.</param>
		/// <returns>The original value of the array element.</returns>
		public static T GetAndAccumulate<T>(this T[] array, long index, T x, in ValueFunc<T, T, T> accumulator)
            where T : class
            => GetAndAccumulate(ref array[index], x, accumulator);

        /// <summary>
		/// Atomically updates the array element with the results 
		/// of applying the given function, returning the updated value.
		/// </summary>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The updated value.</returns>
		public static T UpdateAndGet<T>(this T[] array, long index, Func<T, T> updater)
            where T : class
            => UpdateAndGet(array, index, new ValueFunc<T, T>(updater, true));

        /// <summary>
		/// Atomically updates the array element with the results 
		/// of applying the given function, returning the updated value.
		/// </summary>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The updated value.</returns>
		public static T UpdateAndGet<T>(this T[] array, long index, in ValueFunc<T, T> updater)
            where T : class
            => UpdateAndGet(ref array[index], updater);

        /// <summary>
		/// Atomically updates the array element with the results 
		/// of applying the given function, returning the original value.
		/// </summary>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The original value of the array element.</returns>
		public static T GetAndUpdate<T>(this T[] array, long index, Func<T, T> updater)
            where T : class
            => GetAndUpdate(array, index, new ValueFunc<T, T>(updater, true));

        /// <summary>
		/// Atomically updates the array element with the results 
		/// of applying the given function, returning the original value.
		/// </summary>
		/// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The original value of the array element.</returns>
		public static T GetAndUpdate<T>(this T[] array, long index, in ValueFunc<T, T> updater)
            where T : class
            => GetAndUpdate(ref array[index], updater);
    }

    /// <summary>
    /// Provides container with atomic operations
    /// for the reference type.
    /// </summary>
    /// <typeparam name="T">Type of object to be stored inside of container.</typeparam>
    /// <remarks>
    /// Use this structure in the declaration of integer
    /// value. volatile specifier is not needed for such field.
    /// Do not pass this structure by value into another methods,
    /// otherwise you will get a local copy of the reference
    /// not referred to the field.
    /// </remarks>
    [Serializable]
    [SuppressMessage("Design", "CA1066")]
    [SuppressMessage("Usage", "CA2231")]
    public struct AtomicReference<T> : IEquatable<T>, ISerializable
        where T : class
    {
        private const string ValueSerData = "Value";
        private T value;

        /// <summary>
        /// Initializes a new container with atomic operations 
        /// for the reference type.
        /// </summary>
        /// <param name="value">Initial value to be placed into container.</param>
		public AtomicReference(T value) => this.value = value;

        [SuppressMessage("Usage", "CA1801", Justification = "context is required by .NET serialization framework")]
        private AtomicReference(SerializationInfo info, StreamingContext context)
        {
            value = (T)info.GetValue(ValueSerData, typeof(T));
        }

        /// <summary>
        /// Provides volatile access to the reference value.
        /// </summary>
        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Volatile.Write(ref this.value, value);
        }

        /// <summary>
        /// Compares two values for equality and, if they are equal, 
        /// replaces the stored value.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns>Original (previous) value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T CompareExchange(T expected, T update)
            => Interlocked.CompareExchange(ref value, update, expected);

        /// <summary>
        /// Compares two values for equality and, if they are equal, 
        /// replaces the stored value.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns>true if successful. False return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareAndSet(T expected, T update)
            => AtomicReference.CompareAndSet(ref value, expected, update);

        /// <summary>
        /// Returns textual representation of the stored value.
        /// </summary>
        /// <returns>The textual representation of the stored value.</returns>
		public override string ToString() => Value?.ToString() ?? "NULL";

        /// <summary>
        /// Checks whether the stored value is equal to the given value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if the stored value is equal to <paramref name="other"/> value.</returns>
		public bool Equals(T other) => Equals(other, Value);

        /// <summary>
        /// Checks whether the stored value is equal to the given value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if the stored value is equal to <paramref name="other"/> value.</returns>
        public override bool Equals(object other)
            => other is AtomicReference<T> atomic ? Equals(atomic.Value) : Equals(other as T);

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored value.</returns>
		public override int GetHashCode()
        {
            var value = Value;
            return value is null ? 0 : value.GetHashCode();
        }

        /// <summary>
        /// Modifies value of the container atomically.
        /// </summary>
        /// <param name="update">A new value to be stored inside of container.</param>
        /// <returns>Original value before modification.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetAndSet(T update) => Interlocked.Exchange(ref value, update);

        /// <summary>
        /// Modifies value of the container atomically.
        /// </summary>
        /// <param name="update">A new value to be stored inside of container.</param>
        /// <returns>A new value passed as argument.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T SetAndGet(T update)
        {
            Value = update;
            return update;
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
        public T AccumulateAndGet(T x, Func<T, T, T> accumulator)
            => AtomicReference.AccumulateAndGet(ref value, x, accumulator);

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
        public T AccumulateAndGet(T x, in ValueFunc<T, T, T> accumulator)
            => AtomicReference.AccumulateAndGet(ref value, x, accumulator);

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
        public T GetAndAccumulate(T x, Func<T, T, T> accumulator)
            => AtomicReference.GetAndAccumulate(ref value, x, accumulator);

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
        public T GetAndAccumulate(T x, in ValueFunc<T, T, T> accumulator)
            => AtomicReference.GetAndAccumulate(ref value, x, accumulator);

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        public T UpdateAndGet(Func<T, T> updater)
            => AtomicReference.UpdateAndGet(ref value, updater);

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        public T UpdateAndGet(in ValueFunc<T, T> updater)
            => AtomicReference.UpdateAndGet(ref value, updater);

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        public T GetAndUpdate(Func<T, T> updater)
            => AtomicReference.GetAndUpdate(ref value, updater);

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        public T GetAndUpdate(in ValueFunc<T, T> updater)
            => AtomicReference.GetAndUpdate(ref value, updater);

        /// <summary>
        /// Modifies stored value if it is null.
        /// </summary>
        /// <typeparam name="G">A derived type with default constructor.</typeparam>
        /// <returns>Modified value.</returns>
        public T SetIfNull<G>()
            where G : T, new()
        {
            var value = Value;
            if (value is null)
            {
                value = new G();
                return CompareExchange(null, value) ?? value;
            }
            else
                return value;
        }

        /// <summary>
        /// Modifies stored value if it is null.
        /// </summary>
        /// <param name="supplier">Supplier of a new value.</param>
        /// <returns>Modified value.</returns>
        public T SetIfNull(Func<T> supplier)
        {
            var value = Value;
            if (value is null)
            {
                value = supplier();
                return CompareExchange(null, value) ?? value;
            }
            else
                return value;
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            => info.AddValue(ValueSerData, value, typeof(T));
    }
}
