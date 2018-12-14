using System;
using static System.Runtime.CompilerServices.Unsafe;

namespace MissingPieces
{
	using System.Runtime.CompilerServices;
	using static Reflection.Types;

	public static class ByRef
	{
		public static ByRef<T> AsRef<T>(this ref T value)
			where T : struct
			=> new ByRef<T>(ref value);

		internal static bool IsByRef(Type type)
			=> type != null &&
				type.IsByRef ||
				type.IsGenericInstanceOf(typeof(ByRef<>));
	}

	/// <summary>
	/// Converts generic parameter into ByRef(<typeparamref name="T"/>&amp;) type.
	/// </summary>
	/// <typeparam name="T">Type to be represented as by-ref type.</typeparam>
	public unsafe readonly ref struct ByRef<T>
	{
		/// <summary>
		/// By-ref type.
		/// </summary>
		public static readonly Type Type = typeof(T).MakeByRefType();

		private readonly void* location;

		/// <summary>
		/// Converts managed reference into typed structure.
		/// </summary>
		/// <param name="value">A reference to a value.</param>
		public ByRef(ref T value)
		{
			location = AsPointer(ref value);
		}

		/// <summary>
		/// Gets managed reference to the underlying memory storage.
		/// </summary>
		public ref T Reference
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref AsRef<T>(location);
		}

		/// <summary>
		/// Gets or sets value by reference.
		/// </summary>
		public T Value
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => AsRef<T>(location);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Copy(location, ref value);
		}

		/// <summary>
		/// Converts IN reference into regular reference.
		/// </summary>
		/// <remarks>
		/// This method helps to avoid defensive copy of non-readonly structure
		/// passed as IN parameter.
		/// </remarks>
		/// <param name="value">A reference to convert.</param>
		/// <returns>Converted reference.</returns>
		public static explicit operator ByRef<T>(in T value)
			=> new ByRef<T>(ref AsRef(in value));

		public static implicit operator T(ByRef<T> reference) => reference.Value;
	}
}
