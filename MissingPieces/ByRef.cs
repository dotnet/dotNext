using System;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace MissingPieces
{
	using static Reflection.Types;

	public static class ByRef
	{
		public static ByRef<T> AsRef<T>(this ref T value)
			where T : struct
			=> new ByRef<T>(ref value);

		public static Type GetUnderlyingType(Type byRefType)
		{
			if(byRefType is null)
				return null;
			else if(byRefType.IsByRef)
				return byRefType.GetElementType();
			else if(byRefType.IsGenericInstanceOf(typeof(ByRef<>)))
				return byRefType.GetGenericArguments()[0];
			else
				return null;
		}
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

		/// <summary>
		/// Size of an object of type <typeparamref name="T"/>.
		/// </summary>
		public static readonly int Size = Unsafe.SizeOf<T>();

		private readonly void* location;

		/// <summary>
		/// Converts managed reference into typed structure.
		/// </summary>
		/// <param name="value">A reference to a value.</param>
		public ByRef(ref T value)
		{
			location = Unsafe.AsPointer(ref value);
		}

		/// <summary>
		/// Gets managed reference to the underlying memory storage.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref T GetPinnableReference() => ref Unsafe.AsRef<T>(location);

		/// <summary>
		/// Gets or sets value by reference.
		/// </summary>
		public T Value
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Unsafe.AsRef<T>(location);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Unsafe.Copy(location, ref value);
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
			=> new ByRef<T>(ref Unsafe.AsRef(in value));

		public static implicit operator T(ByRef<T> reference) => reference.Value;

		public static bool operator ==(ByRef<T> first, ByRef<T> second)
			=> first.Equals(second);

		public static bool operator !=(ByRef<T> first, ByRef<T> second)
			=> !first.Equals(second);

		public bool Equals(ByRef<T> other)
			=> location == other.location;

		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool Equals(object other)
		{
			switch (other)
			{
				case IntPtr iptr:
					return iptr == new IntPtr(location);
				case UIntPtr uptr:
					return uptr == new UIntPtr(location);
				default:
					return false;
			}
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => new UIntPtr(location).GetHashCode();

		public override string ToString() => new UIntPtr(location).ToString();
	}
}
