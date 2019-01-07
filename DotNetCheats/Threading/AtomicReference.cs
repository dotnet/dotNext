using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Cheats.Threading
{
	using Generics;

	/// <summary>
	/// Provides atomic operations for the reference type.
	/// </summary>
	public static class AtomicReference
	{
		
		private sealed class CASProvider<T> : Constant<CAS<T>>
			where T : class
		{
			public CASProvider()
				: base(CompareAndSet)
			{
			}
		}

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
		public static T AccumulateAndGet<T>(ref T value, T x, Func<T, T, T> accumulator)
			where T : class
			=> Atomic<T, CASProvider<T>>.Accumulute(ref value, x, accumulator).NewValue;

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
		public static T GetAndAccumulate<T>(ref T value, T x, Func<T, T, T> accumulator)
			where T : class
			=> Atomic<T, CASProvider<T>>.Accumulute(ref value, x, accumulator).OldValue;

		/// <summary>
		/// Atomically updates the stored value with the results 
		/// of applying the given function, returning the updated value.
		/// </summary>
		/// <typeparam name="T">Type of value in the memory storage.</typeparam>
		/// <param name="value">The value to update.</param>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The updated value.</returns>
		public static T UpdateAndGet<T>(ref T value, Func<T, T> updater)
			where T : class
			=> Atomic<T, CASProvider<T>>.Update(ref value, updater).NewValue;

		/// <summary>
		/// Atomically updates the stored value with the results 
		/// of applying the given function, returning the original value.
		/// </summary>
		/// <typeparam name="T">Type of value in the memory storage.</typeparam>
		/// <param name="value">The value to update.</param>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The original value.</returns>
		public static T GetAndUpdate<T>(ref T value, Func<T, T> updater)
			where T : class
			=> Atomic<T, CASProvider<T>>.Update(ref value, updater).OldValue;
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
	public struct AtomicReference<T> : IEquatable<T>
		where T : class
	{
		private T value;

		public AtomicReference(T value)
		{
			this.value = value;
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

		public override string ToString() => Value?.ToString() ?? "NULL";

		public bool Equals(T other) => Equals(other, Value);

		public override bool Equals(object other)
			=> other is AtomicReference<T> atomic ? Equals(atomic.Value) : Equals(other as T);

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
		/// Atomically updates the stored value with the results 
		/// of applying the given function, returning the updated value.
		/// </summary>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The updated value.</returns>
		public T UpdateAndGet(Func<T, T> updater)
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
	}
}
