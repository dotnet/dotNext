using System;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace MissingPieces
{
	/// <summary>
	/// Low-level operations with memory and spans.
	/// </summary>
	public static class Memory
	{
		private static class Ref<T>
		{
			internal delegate ref T InConverter(in T value);

			private static ref T Identity(ref T input) => ref input;

			internal static readonly InConverter Convert = typeof(Ref<T>)
									.GetMethod(nameof(Identity), BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
									.CreateDelegate<InConverter>();
		}
		

		/// <summary>
		/// Converts IN parameter into regular reference.
		/// </summary>
		/// <param name="value">A reference to convert.</param>
		/// <typeparam name="T">Type of reference.</typeparam>
		/// <returns>Converted reference.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T AsRef<T>(in T value) => ref Ref<T>.Convert(in value);

		/// <summary>
		/// Dereferences pointer.
		/// </summary>
		/// <typeparam name="T">Type to read.</typeparam>
		/// <param name="pointer">Non-zero pointer.</param>
		/// <returns>Restored value from the given location.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe T Dereference<T>(IntPtr pointer)
			where T : unmanaged
			=> Unsafe.ReadUnaligned<T>((T*)pointer);

		/// <summary>
		/// Reads a value of type <typeparamref name="T"/> from the given location
		/// and increase pointer according with size of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Type to read.</typeparam>
		/// <param name="pointer">Non-zero pointer.</param>
		/// <returns>Restored value from the given location.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Read<T>(ref IntPtr pointer)
				where T : unmanaged
		{
			var result = Dereference<T>(pointer);
			pointer += Unsafe.SizeOf<T>();
			return result;
		}

		/// <summary>
		/// Computes bitwise equality between two value types.
		/// </summary>
		/// <param name="first">The first structure to compare.</param>
		/// <param name="second">The second structure to compare.</param>
		/// <typeparam name="T1"></typeparam>
		/// <typeparam name="T2"></typeparam>
		/// <returns></returns>
		public static unsafe bool BitwiseEquals<T1, T2>(this T1 first, T2 second)
			where T1: struct
			where T2: struct
			=> new ReadOnlySpan<byte>(Unsafe.AsPointer(ref first), Unsafe.SizeOf<T1>())
				.SequenceEqual(new ReadOnlySpan<byte>(Unsafe.AsPointer(ref second), Unsafe.SizeOf<T2>()));

		public static unsafe int BitwiseCompare<T1, T2>(this T1 first, T2 second)
			where T1: unmanaged
			where T2: unmanaged
			=> new ReadOnlySpan<byte>(Unsafe.AsPointer(ref first), Unsafe.SizeOf<T1>())
				.SequenceCompareTo(new ReadOnlySpan<byte>(Unsafe.AsPointer(ref second), Unsafe.SizeOf<T2>()));
		
		public static bool IsDefault<T>(this T value)
			where T: struct
			=> BitwiseEquals(value, default(T));
	}
}