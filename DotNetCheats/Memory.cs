using System;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNetCheats
{
	/// <summary>
	/// Low-level methods for direct access to memory.
	/// </summary>
	public static class Memory
	{
		public unsafe static T Dereference<T>(this IntPtr pointer)
			where T: unmanaged
			=> *(T*)pointer;

		[CLSCompliant(false)]
		public unsafe static T Dereference<T>(void* pointer)
			where T : unmanaged
			=> *(T*)pointer;

		[CLSCompliant(false)]
		public unsafe static T Read<T>(ref byte* pointer)
			where T : unmanaged
		{
			var result = Dereference<T>(pointer);
			pointer += SizeOf<T>();
			return result;
		}

		public unsafe static T Read<T>(this ref IntPtr pointer)
			where T: unmanaged
		{
			var result = Dereference<T>(pointer);
			pointer += SizeOf<T>();
			return result;
		}
	}
}
