using System;
using System.Runtime.CompilerServices;

namespace MissingPieces
{
	/// <summary>
	/// Represents typed representation of memory allocated on the stack.
	/// </summary>
	/// <typeparam name="T">Type of value to be allocated on the stack.</typeparam>
	public ref struct StackValue<T>
		where T : struct
	{
		/// <summary>
		/// Represents size of <typeparamref name="T"/> when allocated on the stack.
		/// </summary>
		public static readonly int Size = Unsafe.SizeOf<T>();

		/// <summary>
		/// Gets or sets structure allocated on the stack.
		/// </summary>
		public T Value;

		internal StackValue(in T value)
		{
			Value = value;
		}

		/// <summary>
		/// Gets byte value located at the specified position in the stack.
		/// </summary>
		/// <param name="position">Stack offset.</param>
		/// <returns></returns>
		public unsafe byte this[int position]
		{
			get
			{
				if (position < 0 && position >= Size)
					throw new ArgumentOutOfRangeException($"Position should be in range [0, {Size}).");
				var address = new UIntPtr(Address) + position;
				return *(byte*)address;
			}
		}

		/// <summary>
		/// Gets stack pointer.
		/// </summary>
		[CLSCompliant(false)]
		public unsafe void* Address => Unsafe.AsPointer(ref Value);

		public unsafe bool TheSame<U>(in StackValue<U> other) 
			where U: struct
			=> Address == other.Address;

		/// <summary>
		/// Extracts value allocated on the stack.
		/// </summary>
		/// <param name="stack">Stack-allocated value.</param>
		public static implicit operator T(in StackValue<T> stack) => stack.Value;

		/// <summary>
		/// Represents stack memory in the form of read-only span.
		/// </summary>
		/// <param name="stack">Stack-allocated memory.</param>
		public static unsafe implicit operator ReadOnlySpan<byte>(in StackValue<T> stack) => new ReadOnlySpan<byte>(stack.Address, Size);


		public bool Equals(in T value) => Value.Equals(value);
	}
}