using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MissingPieces.Threading
{
	/// <summary>
	/// Various atomic operations for integer data type
	/// accessible as extension methods.
	/// </summary>
	/// <remarks>
	/// Methods exposed by this class provide volatile read/write
	/// of the field even if it is not declared as volatile field.
	/// </remarks>
	/// <see cref="Interlocked"/>
	public static class AtomicInteger
	{
		private sealed class CASProvider : CASProvider<int>
		{
			public CASProvider()
				: base(CompareAndSet)
			{
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int VolatileGet(ref this int value)
			=> Volatile.Read(ref value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void VolatileSet(ref this int value, int newValue)
			=> Volatile.Write(ref value, newValue);

		/// <summary>
		/// Atomically increments by one referenced value.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <returns>Incremented value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IncrementAndGet(ref this int value)
			=> Interlocked.Increment(ref value);

		/// <summary>
		/// Atomically decrements by one the current value.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <returns>Decremented value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int DecrementAndGet(ref this int value)
			=> Interlocked.Decrement(ref value);

		/// <summary>
		/// Compares two 32-bit signed integers for equality and, 
		/// if they are equal, replaces referenced value.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="expected">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns>Original (previous) value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CompareExchange(ref this int value, int expected, int update)
			=> Interlocked.CompareExchange(ref value, update, expected);

		/// <summary>
		/// Atomically sets referenced value to the given updated value if the current value == the expected value.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="expected">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns>true if successful. False return indicates that the actual value was not equal to the expected value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool CompareAndSet(ref this int value, int expected, int update)
			=> CompareExchange(ref value, expected, update) == expected;

		/// <summary>
		/// Adds two 32-bit integers and replaces referenced integer with the sum, 
		/// as an atomic operation.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="operand">The value to be added to the currently stored integer.</param>
		/// <returns>Result of sum operation.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Add(ref this int value, int operand)
			=> Interlocked.Add(ref value, operand);

		/// <summary>
		/// Modifies referenced value of the container atomically.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="update">A new value to be stored inside of container.</param>
		/// <returns>Original value before modification.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetAndSet(ref this int value, int update)
			=> Interlocked.Exchange(ref value, update);

		/// <summary>
		/// Modifies value of the container atomically.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="update">A new value to be stored inside of container.</param>
		/// <returns>A new value passed as argument.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SetAndGet(ref this int value, int update)
		{
			Volatile.Write(ref value, update);
			return update;
		}

		/// <summary>
		/// Atomically updates the current value with the results of applying the given function 
		/// to the current and given values, returning the updated value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the current value as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments</param>
		/// <returns>The updated value.</returns>
		public static int AccumulateAndGet(ref this int value, int x, Func<int, int, int> accumulator)
			=> Atomic<int, CASProvider>.Accumulute(ref value, x, accumulator).NewValue;

		/// <summary>
		/// Atomically updates the current value with the results of applying the given function 
		/// to the current and given values, returning the original value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the current value as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments</param>
		/// <returns>The original value.</returns>
		public static int GetAndAccumulate(ref this int value, int x, Func<int, int, int> accumulator)
			=> Atomic<int, CASProvider>.Accumulute(ref value, x, accumulator).OldValue;

		/// <summary>
		/// Atomically updates the stored value with the results 
		/// of applying the given function, returning the updated value.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The updated value.</returns>
		public static int UpdateAndGet(ref this int value, Func<int, int> updater)
			=> Atomic<int, CASProvider>.Update(ref value, updater).NewValue;

		/// <summary>
		/// Atomically updates the stored value with the results 
		/// of applying the given function, returning the original value.
		/// </summary>
		/// <param name="value">Reference to a value to be modified.</param>
		/// <param name="updater">A side-effect-free function</param>
		/// <returns>The original value.</returns>
		public static int GetAndUpdate(ref this int value, Func<int, int> updater)
			=> Atomic<int, CASProvider>.Update(ref value, updater).OldValue;
	}
}
