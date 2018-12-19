using System;
using System.Runtime.CompilerServices;

namespace MissingPieces
{
	/// <summary>
	/// Represents structured and type-safe representation of memory allocated on the stack.
	/// </summary>
	/// <typeparam name="T">Type of value to be allocated on the stack.</typeparam>
	public readonly ref struct StackValue<T>
		where T : struct
	{
		/// <summary>
		/// Represents size of <typeparamref name="T"/> when allocated on the stack.
		/// </summary>
		public static readonly int Size = Unsafe.SizeOf<T>();

		/// <summary>
		/// Gets structure allocated on the stack.
		/// </summary>
		public readonly T Value;

		/// <summary>
		/// Allocates a new memory on the stack and initialize it with the specified content.
		/// </summary>
		/// <param name="value">The value to be placed onto stack.</param>
		public StackValue(in T value) => this.Value = value;

		/// <summary>
		/// Gets byte value located at the specified position in the stack.
		/// </summary>
		/// <param name="position">Stack offset.</param>
		/// <returns></returns>
		public unsafe byte this[int position]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				if (position < 0 && position >= Size)
					throw new ArgumentOutOfRangeException($"Position should be in range [0, {Size}).");
				var address = new UIntPtr(Address) + position;
				return *(byte*)address;
			}
		}

		/// <summary>
		/// Converts stack-allocated block of memory into byte array allocated on the heap.
		/// </summary>
		/// <returns>A copy of stack-allocated memory in the form of byte array.</returns>
		public byte[] ToArray()
		{
			ReadOnlySpan<byte> span = this;
			return span.ToArray();
		}

		/// <summary>
		/// Gets stack pointer.
		/// </summary>
		[CLSCompliant(false)]
		public unsafe void* Address
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Unsafe.AsPointer(ref Memory.AsRef<T>(in Value));
		}

		/// <summary>
		/// Returns true of other stack-allocated memory points
		/// to the same address.
		/// </summary>
		/// <param name="other">Other stack-allocated value.</param>
		/// <typeparam name="U">Type of stack-allocated value.</typeparam>
		/// <returns>True, if both stack-allocated blocks of memory points to the same address.</returns>
		public unsafe bool TheSame<U>(in StackValue<U> other) 
			where U: struct
			=> Address == other.Address;

		/// <summary>
		/// Checks bitwise equality between this block of memory and specified.
		/// </summary>
		/// <param name="other">Other block of memory to compare.</param>
		/// <typeparam name="U">Structure of other block of memory to compare.</typeparam>
		/// <returns>True, if both memory blocks have the same bits.</returns>
		public unsafe bool Equals<U>(in StackValue<U> other)
			where U: struct
		{
			if(Size != StackValue<U>.Size)
				return false;


			var first = Address;
			var second = other.Address;
			return first == second || Memory.BitwiseEquals(first, second, Size);
		}

		/// <summary>
		/// Extracts value allocated on the stack.
		/// </summary>
		/// <param name="stack">Stack-allocated value.</param>
		public static implicit operator T(in StackValue<T> stack) => stack.Value;

		public static implicit operator StackValue<T>(in T value) => new StackValue<T>(in value);

		/// <summary>
		/// Represents stack memory in the form of read-only span.
		/// </summary>
		/// <param name="stack">Stack-allocated memory.</param>
		public static unsafe implicit operator ReadOnlySpan<byte>(in StackValue<T> stack) => new ReadOnlySpan<byte>(stack.Address, Size);

		public static bool operator==(in StackValue<T> first, in StackValue<T> second) => first.Equals(second);

		public static bool operator!=(in StackValue<T> first, in StackValue<T> second) => !first.Equals(second);

		public bool Equals(in T value) => Value.Equals(value);

		public override bool Equals(object other) => other is T value && Equals(in value);

		public override int GetHashCode() => Value.GetHashCode();

		public override string ToString() => Value.ToString();

		/// <summary>
		/// Performs bitwise equality between two structures.
		/// </summary>
		/// <param name="first">The first structure to compare.</param>
		/// <param name="second">The second structure to compare.</param>
		/// <typeparam name="T">Type of structure.</typeparam>
		/// <returns>True, if both structures have the same set of bits.</returns>
		public static bool BitwiseEquals(in T first, in T second)
			=> new StackValue<T>(in first) == new StackValue<T>(in second);
	}
}