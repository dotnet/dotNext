using System;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading.Tasks;

namespace Cheats.Runtime.InteropServices
{
	/// <summary>
	/// Low-level methods for direct access to memory.
	/// </summary>
	public static class Memory
	{
		private static readonly int BitwiseHashSalt = new Random().Next();

		/// <summary>
		/// Represents null pointer.
		/// </summary>
		[CLSCompliant(false)]
		public static unsafe readonly void* NullPtr = IntPtr.Zero.ToPointer();

		/// <summary>
		/// Reads a value of type <typeparamref name="T"/> from the given location
		/// and adjust pointer according with size of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Unmanaged type to dereference.</typeparam>
		/// <param name="source">A pointer to block of memory.</param>
		/// <returns>Dereferenced value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static T Read<T>(ref IntPtr source)
			where T : unmanaged
		{
			var result = Unsafe.Read<T>(source.ToPointer());
			source += Unsafe.SizeOf<T>();
			return result;
		}

		/// <summary>
		/// Reads a value of type <typeparamref name="T"/> from the given location
		/// without assuming architecture dependent alignment of the addresses;
		/// and adjust pointer according with size of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Unmanaged type to dereference.</typeparam>
		/// <param name="source">A pointer to block of memory.</param>
		/// <returns>Dereferenced value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static T ReadUnaligned<T>(ref IntPtr source)
			where T : unmanaged
		{
			var result = Unsafe.ReadUnaligned<T>(source.ToPointer());
			source += Unsafe.SizeOf<T>();
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static void Write<T>(ref IntPtr destination, T value)
			where T : unmanaged
		{
			Unsafe.Write<T>(destination.ToPointer(), value);
			destination += Unsafe.SizeOf<T>();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static void WriteUnaligned<T>(ref IntPtr destination, T value)
			where T : unmanaged
		{
			Unsafe.WriteUnaligned<T>(destination.ToPointer(), value);
			destination += Unsafe.SizeOf<T>();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[CLSCompliant(false)]
		public static unsafe void Copy(void* source, void* destination, long length)
			=> Buffer.MemoryCopy(source, destination, length, length);
		
		[CLSCompliant(false)]
		public unsafe static void Copy(IntPtr source, IntPtr destination, long length)
			=> Copy(source.ToPointer(), destination.ToPointer(), length);

		public static async Task<long> ReadFromStreamAsync(Stream source, IntPtr destination, long length)
		{
			if(!source.CanRead)
				throw new ArgumentException("Stream is not readable", nameof(source));
			
			var total = 0L;
			for(var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
			{
				var count = await source.ReadAsync(buffer, 0, buffer.Length);
				WriteUnaligned(ref destination, Unsafe.ReadUnaligned<IntPtr>(ref buffer[0]));
				total += count;
				if(count < IntPtr.Size)
					return total;
			}
			while(length > 0)
			{
				var b = source.ReadByte();
				if(b >=0)
				{
					WriteUnaligned<byte>(ref destination, (byte)b);
					length -= sizeof(byte);
					total += sizeof(byte);
				}
				else
					break;
			}
			return total;
		}

		[CLSCompliant(false)]
		public static unsafe Task<long> ReadFromStreamAsync(Stream source, void* destination, long length)
			=> ReadFromStreamAsync(source, new IntPtr(destination), length);

		public static long ReadFromStream(Stream source, IntPtr destination, long length)
		{
			if(!source.CanRead)
				throw new ArgumentException("Stream is not readable", nameof(source));
			
			var total = 0L;
			for(var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
			{
				var count = source.Read(buffer, 0, buffer.Length);
				WriteUnaligned(ref destination, Unsafe.ReadUnaligned<IntPtr>(ref buffer[0]));
				total += count;
				if(count < IntPtr.Size)
					return total;
			}
			while(length > 0)
			{
				var b = source.ReadByte();
				if(b >=0)
				{
					WriteUnaligned<byte>(ref destination, (byte)b);
					length -= sizeof(byte);
					total += sizeof(byte);
				}
				else
					break;
			}
			return total;
		}

		[CLSCompliant(false)]
		public static unsafe long ReadFromStream(Stream source, void* destination, long length)
			=> ReadFromStream(source, new IntPtr(destination), length);
		
		public static void WriteToSteam(IntPtr source, long length, Stream destination)
		{
			if(!destination.CanWrite)
				throw new ArgumentException("Stream is not writable", nameof(destination));

			for(var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
			{
				Unsafe.As<byte, IntPtr>(ref buffer[0]) = ReadUnaligned<IntPtr>(ref source);
				destination.Write(buffer, 0, buffer.Length);
			}
			while(length > 0)
			{
				destination.WriteByte(ReadUnaligned<byte>(ref source));
				length -= sizeof(byte);
			}
		}

		[CLSCompliant(false)]
		public static unsafe void WriteToSteam(void* source, long length, Stream destination)
			=> WriteToSteam(new IntPtr(source), length, destination);

		public static async Task WriteToSteamAsync(IntPtr source, long length, Stream destination)
		{
			if(!destination.CanWrite)
				throw new ArgumentException("Stream is not writable", nameof(destination));

			for(var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
			{
				Unsafe.As<byte, IntPtr>(ref buffer[0]) = ReadUnaligned<IntPtr>(ref source);
				await destination.WriteAsync(buffer, 0, buffer.Length);
			}
			while(length > 0)
			{
				destination.WriteByte(ReadUnaligned<byte>(ref source));
				length -= sizeof(byte);
			}
		}

		[CLSCompliant(false)]
		public static unsafe Task WriteToSteamAsync(void* source, long length, Stream destination)
			=> WriteToSteamAsync(new IntPtr(source), length, destination);

		/// <summary>
		/// Computes hash code for the block of memory, 64-bit version.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="useSalt"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="useSalt">True to include randomized salt data into hashing; false to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		public static unsafe long GetHashCode(IntPtr pointer, long length, long hash, Func<long, long, long> hashFunction, bool useSalt = true)
		{
			while(length > IntPtr.Size)
			{
				hash = hashFunction(hash, ReadUnaligned<IntPtr>(ref pointer).ToInt64());
				length -= IntPtr.Size;
			}
			while(length > 0)
			{
				hash = hashFunction(hash, ReadUnaligned<byte>(ref pointer));
				length -= sizeof(byte);
			}
			
			return useSalt ? hashFunction(hash, BitwiseHashSalt) : hash;
		}

		/// <summary>
		/// Computes hash code for the block of memory, 64-bit version.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="useSalt"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="useSalt">True to include randomized salt data into hashing; false to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		[CLSCompliant(false)]
		public static unsafe long GetHashCode(void* pointer, long length, long hash, Func<long, long, long> hashFunction, bool useSalt = true)
			=> GetHashCode(new IntPtr(pointer), length, hash, hashFunction, useSalt);

		/// <summary>
		/// Computes hash code for the block of memory.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="useSalt"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="useSalt">True to include randomized salt data into hashing; false to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		public static unsafe int GetHashCode(IntPtr source, long length, int hash, Func<int, int, int> hashFunction, bool useSalt = true)
		{
			while(length > sizeof(int))
			{
				hash = hashFunction(hash, ReadUnaligned<int>(ref source));
				length -= sizeof(int);
			}
			while(length > 0)
			{
				hash = hashFunction(hash, ReadUnaligned<byte>(ref source));
				length -= sizeof(byte);
			}
			
			return useSalt ? hashFunction(hash, BitwiseHashSalt) : hash;
		}
		
		/// <summary>
		/// Computes hash code for the block of memory.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="useSalt"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="useSalt">True to include randomized salt data into hashing; false to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		[CLSCompliant(false)]
		public static unsafe int GetHashCode(void* source, long length, int hash, Func<int, int, int> hashFunction, bool useSalt = true)
			=> GetHashCode(new IntPtr(source), length, hash, hashFunction, useSalt);
		
		internal static unsafe int GetHashCode(void* source, long length)
			=> GetHashCode(source, length, unchecked((int)2166136261), (hash, word) => (hash ^ word) * 16777619);

		/// <summary>
		/// Computes equality between two blocks of memory.
		/// </summary>
		/// <param name="first">A pointer to the first memory block.</param>
		/// <param name="second">A pointer to the second memory block.</param>
		/// <param name="length">Length of first and second memory blocks, in bytes.</param>
		/// <returns>True, if both memory blocks have the same data; otherwise, false.</returns>
		[CLSCompliant(false)]
		public static unsafe bool Equals(void* first, void* second, int length)
			=> new ReadOnlySpan<byte>(first, length).SequenceEqual(new ReadOnlySpan<byte>(second, length));

		/// <summary>
		/// Computes equality between two blocks of memory.
		/// </summary>
		/// <param name="first">A pointer to the first memory block.</param>
		/// <param name="second">A pointer to the second memory block.</param>
		/// <param name="length">Length of first and second memory blocks, in bytes.</param>
		/// <returns>True, if both memory blocks have the same data; otherwise, false.</returns>
		public static unsafe bool Equals(IntPtr first, IntPtr second, int length)
			=> Equals(first.ToPointer(), second.ToPointer(), length);
		
		[CLSCompliant(false)]
		public static unsafe int Compare(void* first, void* second, int length)
			=> new ReadOnlySpan<byte>(first, length).SequenceCompareTo(new ReadOnlySpan<byte>(second, length));

		public static unsafe int Compare(IntPtr first, IntPtr second, int length)
			=> Compare(first.ToPointer(), second.ToPointer(), length);
	}
}
