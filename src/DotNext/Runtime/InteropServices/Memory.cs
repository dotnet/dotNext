using System;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading.Tasks;

namespace DotNext.Runtime.InteropServices
{
	/// <summary>
	/// Low-level methods for direct memory access.
	/// </summary>
    /// <remarks>
    /// Methods in this class doesn't perform
    /// any safety check. Incorrect usage of them may destabilize
    /// Common Language Runtime.
    /// </remarks>
	public static class Memory
	{
		private static class FNV1a
		{
			internal const int Offset = unchecked((int)2166136261);
			private const int Prime = 16777619;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal static int HashRound(int hash, int data) => (hash ^ data) * Prime;
		}
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

        /// <summary>
        /// Writes a value into the address using aligned access
        /// and adjust address according with size of
        /// type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type.</typeparam>
        /// <param name="destination">Destination address.</param>
        /// <param name="value">The value to write into the address.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static void Write<T>(ref IntPtr destination, T value)
			where T : unmanaged
		{
			Unsafe.Write(destination.ToPointer(), value);
			destination += Unsafe.SizeOf<T>();
		}

        /// <summary>
        /// Writes a value into the address using unaligned access
        /// and adjust address according with size of
        /// type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type.</typeparam>
        /// <param name="destination">Destination address.</param>
        /// <param name="value">The value to write into the address.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static void WriteUnaligned<T>(ref IntPtr destination, T value)
			where T : unmanaged
		{
			Unsafe.WriteUnaligned(destination.ToPointer(), value);
			destination += Unsafe.SizeOf<T>();
		}

        /// <summary>
        /// Copies specified number of bytes from one address in memory to another.
        /// </summary>
        /// <param name="source">The address of the bytes to copy.</param>
        /// <param name="destination">The target address.</param>
        /// <param name="length">The number of bytes to copy from source address to destination.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[CLSCompliant(false)]
		public unsafe static void Copy(void* source, void* destination, long length)
			=> Buffer.MemoryCopy(source, destination, length, length);

        /// <summary>
        /// Copies specified number of bytes from one address in memory to another.
        /// </summary>
        /// <param name="source">The address of the bytes to copy.</param>
        /// <param name="destination">The target address.</param>
        /// <param name="length">The number of bytes to copy from source address to destination.</param>
		public unsafe static void Copy(IntPtr source, IntPtr destination, long length)
			=> Copy(source.ToPointer(), destination.ToPointer(), length);

        /// <summary>
        /// Copies specified number of bytes from stream into memory.
        /// </summary>
        /// <remarks>
        /// Reading from stream is performed asynchronously.
        /// </remarks>
        /// <param name="source">The source of bytes.</param>
        /// <param name="destination">The pointer to destination memory block.</param>
        /// <param name="length">The number of bytes to copy from source stream into destination memory block.</param>
        /// <returns>The task representing state of copy operation and returning number of bytes copied.</returns>
		public static async Task<long> ReadFromStreamAsync(Stream source, IntPtr destination, long length)
		{
			if(!source.CanRead)
				throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));
			
			var total = 0L;
			for(var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
			{
				var count = await source.ReadAsync(buffer, 0, buffer.Length);
				WriteUnaligned(ref destination, Unsafe.ReadUnaligned<IntPtr>(ref buffer[0]));
				total += count;
				if(count < IntPtr.Size)
					return total;
				buffer.Initialize();
			}
			while(length > 0)
			{
				var b = source.ReadByte();
				if(b >=0)
				{
					Write(ref destination, (byte)b);
					length -= sizeof(byte);
					total += sizeof(byte);
				}
				else
					break;
			}
			return total;
		}

        /// <summary>
        /// Copies specified number of bytes from stream into memory.
        /// </summary>
        /// <remarks>
        /// Reading from stream is performed asynchronously.
        /// </remarks>
        /// <param name="source">The source of bytes.</param>
        /// <param name="destination">The pointer to destination memory block.</param>
        /// <param name="length">The number of bytes to copy from source stream into destination memory block.</param>
        /// <returns>The task representing state of copy operation and returning number of bytes copied.</returns>
        [CLSCompliant(false)]
		public unsafe static Task<long> ReadFromStreamAsync(Stream source, void* destination, long length)
			=> ReadFromStreamAsync(source, new IntPtr(destination), length);

        /// <summary>
        /// Copies specified number of bytes from stream into memory.
        /// </summary>
        /// <remarks>
        /// Reading from stream is performed synchronously.
        /// </remarks>
        /// <param name="source">The source of bytes.</param>
        /// <param name="destination">The pointer to destination memory block.</param>
        /// <param name="length">The number of bytes to copy from source stream into destination memory block.</param>
        /// <returns>The number of bytes copied.</returns>
        public static long ReadFromStream(Stream source, IntPtr destination, long length)
		{
			if(!source.CanRead)
				throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));
			
			var total = 0L;
			for(var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
			{
				var count = source.Read(buffer, 0, buffer.Length);
				WriteUnaligned(ref destination, Unsafe.ReadUnaligned<IntPtr>(ref buffer[0]));
				total += count;
				if(count < IntPtr.Size)
					return total;
				buffer.Initialize();
			}
			while(length > 0)
			{
				var b = source.ReadByte();
				if(b >=0)
				{
					Write(ref destination, (byte)b);
					length -= sizeof(byte);
					total += sizeof(byte);
				}
				else
					break;
			}
			return total;
		}

        /// <summary>
        /// Copies specified number of bytes from stream into memory.
        /// </summary>
        /// <remarks>
        /// Reading from stream is performed synchronously.
        /// </remarks>
        /// <param name="source">The source of bytes.</param>
        /// <param name="destination">The pointer to destination memory block.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <returns>The number of bytes copied.</returns>
        [CLSCompliant(false)]
		public unsafe static long ReadFromStream(Stream source, void* destination, long length)
			=> ReadFromStream(source, new IntPtr(destination), length);
		
        /// <summary>
        /// Copies specified number of bytes memory into stream.
        /// </summary>
        /// <remarks>
        /// Writing to stream is performed synchronously.
        /// </remarks>
        /// <param name="source">The pointer to source memory block.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="destination">The stream to write into.</param>
		public static void WriteToSteam(IntPtr source, long length, Stream destination)
		{
			if(!destination.CanWrite)
				throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));

			for(var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
			{
				Unsafe.As<byte, IntPtr>(ref buffer[0]) = ReadUnaligned<IntPtr>(ref source);
				destination.Write(buffer, 0, buffer.Length);
			}
			while(length > 0)
			{
				destination.WriteByte(Read<byte>(ref source));
				length -= sizeof(byte);
			}
		}

        /// <summary>
        /// Copies specified number of bytes memory into stream.
        /// </summary>
        /// <remarks>
        /// Writing to stream is performed synchronously.
        /// </remarks>
        /// <param name="source">The pointer to source memory block.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="destination">The stream to write into.</param>
        [CLSCompliant(false)]
		public unsafe static void WriteToSteam(void* source, long length, Stream destination)
			=> WriteToSteam(new IntPtr(source), length, destination);

        /// <summary>
        /// Copies specified number of bytes memory into stream.
        /// </summary>
        /// <remarks>
        /// Writing to stream is performed asynchronously.
        /// </remarks>
        /// <param name="source">The pointer to source memory block.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="destination">The stream to write into.</param>
        /// <returns>The task representing asynchronous state of copying.</returns>
        public static async Task WriteToSteamAsync(IntPtr source, long length, Stream destination)
		{
			if(!destination.CanWrite)
				throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));

			for(var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
			{
				Unsafe.As<byte, IntPtr>(ref buffer[0]) = ReadUnaligned<IntPtr>(ref source);
				await destination.WriteAsync(buffer, 0, buffer.Length);
			}
			while(length > 0)
			{
				destination.WriteByte(Read<byte>(ref source));
				length -= sizeof(byte);
			}
		}

        /// <summary>
        /// Copies specified number of bytes memory into stream.
        /// </summary>
        /// <remarks>
        /// Writing to stream is performed asynchronously.
        /// </remarks>
        /// <param name="source">The pointer to source memory block.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="destination">The stream to write into.</param>
        /// <returns>The task representing asynchronous state of copying.</returns>
        [CLSCompliant(false)]
		public unsafe static Task WriteToSteamAsync(void* source, long length, Stream destination)
			=> WriteToSteamAsync(new IntPtr(source), length, destination);

		/// <summary>
		/// Computes hash code for the block of memory, 64-bit version.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="salted"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		public static unsafe long GetHashCode(IntPtr source, long length, long hash, Func<long, long, long> hashFunction, bool salted = true)
		{
			switch(length)
			{
				case sizeof(byte):
					hash = hashFunction(hash, Read<byte>(ref source));
					break;
				case sizeof(short):
					hash = hashFunction(hash, ReadUnaligned<short>(ref source));
					break;
				default:
					while(length >= IntPtr.Size)
					{
						hash = hashFunction(hash, ReadUnaligned<IntPtr>(ref source).ToInt64());
						length -= IntPtr.Size;
					}
					while(length > 0)
					{
						hash = hashFunction(hash, Read<byte>(ref source));
						length -= sizeof(byte);
					}
					break;
			}
			return salted ? hashFunction(hash, BitwiseHashSalt) : hash;
		}

		/// <summary>
		/// Computes hash code for the block of memory, 64-bit version.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="salted"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		[CLSCompliant(false)]
		public unsafe static long GetHashCode(void* source, long length, long hash, Func<long, long, long> hashFunction, bool salted = true)
			=> GetHashCode(new IntPtr(source), length, hash, hashFunction, salted);

		/// <summary>
		/// Computes hash code for the block of memory.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="salted"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		public static int GetHashCode(IntPtr source, long length, int hash, Func<int, int, int> hashFunction, bool salted = true)
		{
			switch(length)
			{
				case sizeof(byte):
					hash = hashFunction(hash, ReadUnaligned<byte>(ref source));
					break;
				case sizeof(short):
					hash = hashFunction(hash, ReadUnaligned<short>(ref source));
					break;
				default:
					while(length >= sizeof(int))
					{
						hash = hashFunction(hash, ReadUnaligned<int>(ref source));
						length -= sizeof(int);
					}
					while(length > 0)
					{
						hash = hashFunction(hash, Read<byte>(ref source));
						length -= sizeof(byte);
					}
					break;
			}
			return salted ? hashFunction(hash, BitwiseHashSalt) : hash;
		}
		
		/// <summary>
		/// Computes hash code for the block of memory.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="salted"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		[CLSCompliant(false)]
		public unsafe static int GetHashCode(void* source, long length, int hash, Func<int, int, int> hashFunction, bool salted = true)
			=> GetHashCode(new IntPtr(source), length, hash, hashFunction, salted);

        /// <summary>
        /// Computes hash code for the block of memory.
        /// </summary>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <remarks>
        /// This method uses FNV-1a hash algorithm.
        /// </remarks>
        /// <returns>Content hash code.</returns>
        /// <seealso href="http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a">FNV-1a</seealso>
        public static int GetHashCode(IntPtr source, long length, bool salted = true)
		{
			var hash = FNV1a.Offset;
			switch(length)
			{
				case sizeof(byte):
					hash = FNV1a.HashRound(FNV1a.Offset, ReadUnaligned<byte>(ref source));
					break;
				case sizeof(short):
					hash = FNV1a.HashRound(FNV1a.Offset, ReadUnaligned<short>(ref source));
					break;
				default:
					while(length >= sizeof(int))
					{
						hash = FNV1a.HashRound(hash, ReadUnaligned<int>(ref source));
						length -= sizeof(int);
					}
					while(length > 0)
					{
						hash = FNV1a.HashRound(hash, Read<byte>(ref source));
						length -= sizeof(byte);
					}
					break;
			}
			return salted ? FNV1a.HashRound(hash, BitwiseHashSalt) : hash;
		}

        /// <summary>
        /// Computes hash code for the block of memory.
        /// </summary>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <remarks>
        /// This method uses FNV-1a hash algorithm.
        /// </remarks>
        /// <returns>Content hash code.</returns>
        /// <seealso href="http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a">FNV-1a</seealso>
        [CLSCompliant(false)]
		public unsafe static int GetHashCode(void* source, long length, bool salted = true)
			=> GetHashCode(new IntPtr(source), length, salted);

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <remarks>
        /// This method has the same behavior as <see cref="Unsafe.InitBlockUnaligned(void*, byte, uint)"/> but
        /// without restriction on <see cref="uint"/> data type for the length of the memory block.
        /// </remarks>
        /// <param name="ptr">The pointer to the memory to be cleared.</param>
        /// <param name="length">The length of the memory to be cleared.</param>
        public unsafe static void ZeroMem(IntPtr ptr, long length)
        {
            do
            {
                var count = (int)length.UpperBounded(int.MaxValue);
                Unsafe.InitBlockUnaligned(ptr.ToPointer(), 0, (uint)count);
                ptr += count;
                length -= count;
            } while (length > 0);
        }

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <param name="ptr">The pointer to the memory to be cleared.</param>
        /// <param name="length">The length of the memory to be cleared.</param>
        [CLSCompliant(false)]
        public unsafe static void ZeroMem(void* ptr, long length) => ZeroMem(new IntPtr(ptr), length);

        /// <summary>
        /// Computes equality between two blocks of memory.
        /// </summary>
        /// <param name="first">A pointer to the first memory block.</param>
        /// <param name="second">A pointer to the second memory block.</param>
        /// <param name="length">Length of first and second memory blocks, in bytes.</param>
        /// <returns>True, if both memory blocks have the same data; otherwise, false.</returns>
        [CLSCompliant(false)]
        [Obsolete("Use overloaded method with long length")]
        public unsafe static bool Equals(void* first, void* second, int length) => Equals(first, second, (long)length);

        /// <summary>
		/// Computes equality between two blocks of memory.
		/// </summary>
		/// <param name="first">A pointer to the first memory block.</param>
		/// <param name="second">A pointer to the second memory block.</param>
		/// <param name="length">Length of first and second memory blocks, in bytes.</param>
		/// <returns>True, if both memory blocks have the same data; otherwise, false.</returns>
		[CLSCompliant(false)]
        public unsafe static bool Equals(void* first, void* second, long length) => Equals(new IntPtr(first), new IntPtr(second), length);

        /// <summary>
        /// Computes equality between two blocks of memory.
        /// </summary>
        /// <param name="first">A pointer to the first memory block.</param>
        /// <param name="second">A pointer to the second memory block.</param>
        /// <param name="length">Length of first and second memory blocks, in bytes.</param>
        /// <returns>True, if both memory blocks have the same data; otherwise, false.</returns>
        [Obsolete("Use overloaded method with long length")]
        public unsafe static bool Equals(IntPtr first, IntPtr second, int length)
			=> Equals(first.ToPointer(), second.ToPointer(), length);

        /// <summary>
        /// Computes equality between two blocks of memory.
        /// </summary>
        /// <param name="first">A pointer to the first memory block.</param>
        /// <param name="second">A pointer to the second memory block.</param>
        /// <param name="length">Length of first and second memory blocks, in bytes.</param>
        /// <returns>True, if both memory blocks have the same data; otherwise, false.</returns>
        public unsafe static bool Equals(IntPtr first, IntPtr second, long length)
        {
            if (first == second)
                return true;
            switch (length)
            {
                case 0L:
                    return true;
                case sizeof(byte):
                    return Unsafe.Read<byte>(first.ToPointer()) == Unsafe.ReadUnaligned<byte>(second.ToPointer());
                case sizeof(ushort):
                    return Unsafe.ReadUnaligned<ushort>(first.ToPointer()) == Unsafe.ReadUnaligned<ushort>(second.ToPointer());
                case sizeof(uint):
                    return Unsafe.ReadUnaligned<uint>(first.ToPointer()) == Unsafe.ReadUnaligned<uint>(second.ToPointer());
                case sizeof(ulong):
                    return Unsafe.ReadUnaligned<ulong>(first.ToPointer()) == Unsafe.ReadUnaligned<ulong>(second.ToPointer());
                default:
                    do
                    {
                        var count = (int)length.UpperBounded(int.MaxValue);
                        if (new ReadOnlySpan<byte>(first.ToPointer(), count).SequenceEqual(new ReadOnlySpan<byte>(second.ToPointer(), count)))
                        {
                            first += count;
                            second += count;
                            length -= count;
                        }
                        else
                            return false;
                    } while (length > 0);
                    return true;
            }
        }

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="first">The pointer to the first memory block.</param>
        /// <param name="second">The pointer to the second memory block.</param>
        /// <param name="length">The length of the first and second memory blocks.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        [CLSCompliant(false)]
        [Obsolete("Use overloaded method with long length")]
        public unsafe static int Compare(void* first, void* second, int length) => Compare(first, second, (long)length);

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="first">The pointer to the first memory block.</param>
        /// <param name="second">The pointer to the second memory block.</param>
        /// <param name="length">The length of the first and second memory blocks.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        [Obsolete("Use overloaded method with long length")]
        public unsafe static int Compare(IntPtr first, IntPtr second, int length)
			=> Compare(first.ToPointer(), second.ToPointer(), length);

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="first">The pointer to the first memory block.</param>
        /// <param name="second">The pointer to the second memory block.</param>
        /// <param name="length">The length of the first and second memory blocks.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public unsafe static int Compare(IntPtr first, IntPtr second, long length)
        {
            if (first == second)
                return 0;
            switch (length)
            {
                case 0L:
                    return 0;
                case sizeof(byte):
                    return Unsafe.Read<byte>(first.ToPointer()).CompareTo(Unsafe.Read<byte>(second.ToPointer()));
                case sizeof(ushort):
                    return Unsafe.ReadUnaligned<ushort>(first.ToPointer()).CompareTo(Unsafe.ReadUnaligned<ushort>(second.ToPointer()));
                case sizeof(uint):
                    return Unsafe.ReadUnaligned<uint>(first.ToPointer()).CompareTo(Unsafe.ReadUnaligned<uint>(second.ToPointer()));
                case sizeof(ulong):
                    return Unsafe.ReadUnaligned<ulong>(first.ToPointer()).CompareTo(Unsafe.ReadUnaligned<ulong>(second.ToPointer()));
                default:
                    var comparison = 0;
                    do
                    {
                        var count = (int)length.UpperBounded(int.MaxValue);
                        comparison = new ReadOnlySpan<byte>(first.ToPointer(), count).SequenceCompareTo(new ReadOnlySpan<byte>(second.ToPointer(), count));
                        if (comparison == 0)
                        {
                            first += count;
                            second += count;
                            length -= count;
                        }
                        else
                            break;
                    } while (length > 0);
                    return comparison;
            }
        }

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="first">The pointer to the first memory block.</param>
        /// <param name="second">The pointer to the second memory block.</param>
        /// <param name="length">The length of the first and second memory blocks.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        [CLSCompliant(false)]
        public unsafe static int Compare(void* first, void* second, long length) => Compare(new IntPtr(first), new IntPtr(second), length);

        /// <summary>
        /// Indicates that two managed pointers are equal.
        /// </summary>
        /// <typeparam name="T">Type of managed pointer.</typeparam>
        /// <param name="first">The first managed pointer.</param>
        /// <param name="second">The second managed pointer.</param>
        /// <returns><see langword="true"/>, if both managed pointers are equal; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public static bool AreSame<T>(in T first, in T second)
			=> Unsafe.AreSame(ref Unsafe.AsRef(in first), ref Unsafe.AsRef(in second));

        /// <summary>
        /// Returns address of the managed pointer to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type of managed pointer.</typeparam>
        /// <param name="value">Managed pointer to convert into address.</param>
        /// <returns>The address for the managed pointer.</returns>
        /// <remarks>
        /// This method converts managed pointer into address,
        /// not the address of the object itself.
        /// </remarks>
        [CLSCompliant(false)]
        public unsafe static IntPtr AddressOf<T>(in T value)
			=> new IntPtr(Unsafe.AsPointer(ref Unsafe.AsRef(in value)));
	}
}
