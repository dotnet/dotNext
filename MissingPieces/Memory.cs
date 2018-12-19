using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace MissingPieces
{
	/// <summary>
	/// Low-level operations with memory and spans.
	/// </summary>
	public static class Memory
	{
		private static class Ref<T>
		{
			internal delegate ref T Converter(in T value);

			internal static readonly Converter ToRegularRef;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static ref T Identity(ref T value) => ref value;

			static Ref()
			{
				var parameter = Parameter(typeof(T).MakeByRefType());
				var identity = typeof(Ref<T>).GetMethod(nameof(Identity), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				ToRegularRef = Lambda<Converter>(Call(null, identity, parameter), parameter).Compile();
			}
		}

		/// <summary>
		/// Converts IN parameter into regular reference.
		/// </summary>
		/// <param name="value">A reference to convert.</param>
		/// <typeparam name="T">Type of reference.</typeparam>
		/// <returns>Converted reference.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T AsRef<T>(in T value) => ref Ref<T>.ToRegularRef(in value);

		/// <summary>
		/// Dereferences pointer.
		/// </summary>
		/// <typeparam name="T">Type to read.</typeparam>
		/// <param name="pointer">Non-zero pointer.</param>
		/// <returns>Restored value from the given location.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe T Dereference<T>(IntPtr pointer)
			where T : unmanaged
			=> *(T*)pointer;

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

		internal static unsafe bool BitwiseEquals(void* first, void* second, int length)
		{
			var span1 = new ReadOnlySpan<byte>(first, length);
			return span1.SequenceEqual(new ReadOnlySpan<byte>(second, length));

			/*tail_call: switch (count)
			{
				case 0:
					return true;
				case sizeof(byte):
					return Dereference<byte>(first) == Dereference<byte>(second);
				case sizeof(ushort):
					return Dereference<ushort>(first) == Dereference<ushort>(second);
				case sizeof(ushort) + sizeof(byte):
					return Read<ushort>(ref first) == Read<ushort>(ref second) &&
						Dereference<byte>(first) == Dereference<byte>(second);
				case sizeof(uint):
					return Dereference<uint>(first) == Dereference<uint>(second);
				case sizeof(uint) + sizeof(byte):
					return Read<uint>(ref first) == Read<uint>(ref second) &&
						Dereference<byte>(first) == Dereference<byte>(second);
				case sizeof(uint) + sizeof(ushort):
					return Read<uint>(ref first) == Read<uint>(ref second) &&
						Dereference<ushort>(first) == Dereference<ushort>(second);
				case sizeof(uint) + sizeof(ushort) + sizeof(byte):
					return Read<uint>(ref first) == Read<uint>(ref second) &&
						Read<ushort>(ref first) == Read<ushort>(ref second) &&
						Dereference<byte>(first) == Dereference<byte>(second);
				case sizeof(ulong):
					return Dereference<ulong>(first) == Dereference<ulong>(second);
				default:
					if (count > UIntPtr.Size)
					{
						count -= UIntPtr.Size;
						if (Read<UIntPtr>(ref first) == Read<UIntPtr>(ref second))
							goto tail_call;
					}
					else
					{
						count -= sizeof(ulong);
						if (Read<ulong>(ref first) == Read<ulong>(ref second))
							goto tail_call;
					}
					return false;
			}*/
		}
	}
}