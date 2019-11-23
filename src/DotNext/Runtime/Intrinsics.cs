using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using Debug = System.Diagnostics.Debug;
using M = InlineIL.MethodRef;
using Var = InlineIL.LocalVar;

namespace DotNext.Runtime
{
    /// <summary>
    /// Represents highly optimized runtime intrinsic methods.
    /// </summary>
    public static class Intrinsics
    {
        private static class FNV1a32
        {
            internal const int Offset = unchecked((int)2166136261);
            private const int Prime = 16777619;

            internal static int GetHashCode(int hash, int data) => (hash ^ data) * Prime;
        }

        private static class FNV1a64
        {
            internal const long Offset = unchecked((long)14695981039346656037);
            private const long Prime = 1099511628211;

            internal static long GetHashCode(long hash, long data) => (hash ^ data) * Prime;
        }

        /// <summary>
        /// Provides the fast way to check whether the specified type accepts  <see langword="null"/> value as valid value.
        /// </summary>
        /// <remarks>
        /// This method always returns <see langword="true"/> for all reference types and <see cref="Nullable{T}"/>.
        /// On mainstream implementations of .NET CLR, this method is replaced by constant value by JIT compiler with zero runtime overhead.
        /// </remarks>
        /// <typeparam name="T">The type to check.</typeparam>
        /// <returns><see langword="true"/> if <typeparamref name="T"/> is nullable type; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullable<T>()
        {
            const string DefaultVar = "default";
            DeclareLocals(true, new Var(DefaultVar, typeof(T)));
            Ldloc(DefaultVar);
            Box(typeof(T));
            Ldnull();
            Ceq();
            return Return<bool>();
        }

        /// <summary>
        /// Returns default value of the given type.
        /// </summary>
        /// <remarks>
        /// This method helps to avoid generation of temporary variables
        /// necessary for <c>default</c> keyword implementation.
        /// </remarks>
        /// <typeparam name="T">The type for which default value should be obtained.</typeparam>
        /// <returns>The default value of type <typeparamref name="T"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T DefaultOf<T>()
        {
            DeclareLocals(true, new Var(typeof(T)));
            Ldloc_0();
            return Return<T>();
        }

        /// <summary>
        /// Obtain a value of type <typeparamref name="TResult"/> by 
        /// reinterpreting the object representation of <typeparamref name="T"/>. 
        /// </summary>
        /// <remarks>
        /// Every bit in the value representation of the returned <typeparamref name="TResult"/> object 
        /// is equal to the corresponding bit in the object representation of <typeparamref name="T"/>. 
        /// The values of padding bits in the returned <typeparamref name="TResult"/> object are unspecified. 
        /// The method takes into account size of <typeparamref name="T"/> and <typeparamref name="TResult"/> types
        /// and able to provide conversion between types of different size. However, the result may very between
        /// CPU architectures if size of types is different.
        /// </remarks>
        /// <param name="input">A value to convert.</param>
        /// <param name="output">Conversion result.</param>
        /// <typeparam name="T">The value type to be converted.</typeparam>
        /// <typeparam name="TResult">The type of output struct.</typeparam>
        public static void Bitcast<T, TResult>(in T input, out TResult output)
            where T : unmanaged
            where TResult : unmanaged
        {
            //ldobj/stobj pair is used instead of cpobj because this instruction
            //has unspecified behavior if src is not assignable to dst, ECMA-335 III.4.4
            const string slowPath = "slow";
            Ldarg(nameof(output));
            Sizeof(typeof(T));
            Sizeof(typeof(TResult));
            Blt_Un(slowPath);
            //copy from input into output as-is
            Ldarg(nameof(input));
            Ldobj(typeof(TResult));
            Stobj(typeof(TResult));
            Ret();

            MarkLabel(slowPath);
            Dup();
            Initobj(typeof(TResult));
            Ldarg(nameof(input));
            Ldobj(typeof(T));
            Stobj(typeof(T));
            Ret();
            throw Unreachable();    //output must be defined within scope
        }

        /// <summary>
        /// Indicates that specified value type is the default value.
        /// </summary>
        /// <param name="value">Value to check.</param>
        /// <returns><see langword="true"/>, if value is default value; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefault<T>(T value)
        {
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            switch (size)
            {
                default:
                    Push(ref value);
                    Push(size);
                    Call(new M(typeof(Intrinsics), nameof(IsZero)));
                    break;
                case sizeof(byte):
                    Push(ref value);
                    Ldind_I1();
                    Ldc_I4_0();
                    Ceq();
                    break;
                case sizeof(ushort):
                    Push(ref value);
                    Ldind_I2();
                    Ldc_I4_0();
                    Ceq();
                    break;
                case sizeof(uint):
                    Push(ref value);
                    Ldind_I4();
                    Ldc_I4_0();
                    Ceq();
                    break;
                case sizeof(ulong):
                    Push(ref value);
                    Ldind_I8();
                    Ldc_I8(0L);
                    Ceq();
                    break;
            }
            return Return<bool>();
        }

        /// <summary>
        /// Returns the runtime handle associated with type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type which runtime handle should be obtained.</typeparam>
        /// <returns>The runtime handle representing type <typeparamref name="T"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeHandle TypeOf<T>()
        {
            Ldtoken(typeof(T));
            return Return<RuntimeTypeHandle>();
        }

        internal static void UnsafeDispose(object disposable)
        {
            Debug.Assert(disposable is IDisposable);
            Push(disposable);
            Callvirt(new M(typeof(IDisposable), nameof(IDisposable.Dispose)));
            Ret();
        }

        internal static void UnsafeInvoke(object action)
        {
            Debug.Assert(action is Action);
            Push(action);
            Callvirt(new M(typeof(Action), nameof(Action.Invoke)));
            Ret();
        }

        /// <summary>
        /// Determines whether one or more bit fields are set in the given value.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="flag">An enumeration value.</param>
        /// <returns><see langword="true"/> if the bit field or bit fields that are set in <paramref name="flag"/> are also set in <paramref name="value"/>; otherwise, <see langword="false"/>.</returns>
        public static bool HasFlag<T>(T value, T flag)
            where T : struct, Enum
        {
            const string size8Bytes = "8bytes";
            const string size4Bytes = "4bytes";
            const string size2Bytes = "2bytes";
            const string size1Byte = "1byte";
            const string fallback = "fallback";
            Sizeof(typeof(T));
            Switch(
                fallback,   //0 bytes
                size1Byte,  //1 byte
                size2Bytes, //2 bytes
                fallback,   //3 bytes
                size4Bytes, //4 bytes
                fallback,   //5 bytes
                fallback,   //6 bytes
                fallback,   //7 bytes
                size8Bytes //8 bytes
                );

            MarkLabel(fallback);
            Push(ref value);
            Push(flag);
            Box(typeof(T));
            Constrained(typeof(T));
            Callvirt(new M(typeof(Enum), nameof(Enum.HasFlag), typeof(Enum)));
            Ret();

            MarkLabel(size1Byte);
            Push(ref value);
            Ldind_U1();
            Push(ref flag);
            Ldind_U1();
            And();
            Ldc_I4_0();
            Cgt_Un();
            Ret();

            MarkLabel(size2Bytes);
            Push(ref value);
            Ldind_U2();
            Push(ref flag);
            Ldind_U2();
            And();
            Ldc_I4_0();
            Cgt_Un();
            Ret();

            MarkLabel(size4Bytes);
            Push(ref value);
            Ldind_U4();
            Push(ref flag);
            Ldind_U4();
            And();
            Ldc_I4_0();
            Cgt_Un();
            Ret();

            MarkLabel(size8Bytes);
            Push(ref value);
            Ldind_I8();
            Push(ref flag);
            Ldind_I8();
            And();
            Conv_U8();
            Ldc_I4_0();
            Conv_U8();
            Cgt_Un();
            return Return<bool>();
        }

        internal static E GetTupleItem<T, E>(ref T tuple, int index)
            where T : struct, ITuple
        {
            if (index < 0 || index >= tuple.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            Push(ref tuple);
            Sizeof(typeof(E));
            Push(index);
            Conv_U4();
            Mul_Ovf_Un();
            Add();
            Ldobj(typeof(E));
            return Return<E>();
        }

        //throw InvalidCastException for reference type as well as for value type
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNull]
        internal static T Cast<T>(object? obj)
        {
            const string notNull = "notNull";
            Push(obj);
            Isinst(typeof(T));
            Dup();
            Brtrue(notNull);
            Pop();
            Newobj(M.Constructor(typeof(InvalidCastException)));
            Throw();

            MarkLabel(notNull);
            Unbox_Any(typeof(T));
            return Return<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int PointerHashCode(void* pointer)
        {
            Ldarga(nameof(pointer));
            Call(new M(typeof(UIntPtr), nameof(UIntPtr.GetHashCode)));
            return Return<int>();
        }

        /// <summary>
        /// Returns an address of the given by-ref parameter.
        /// </summary>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <param name="value">The object whose address is obtained.</param>
        /// <returns>An address of the given object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr AddressOf<T>(in T value)
        {
            Ldarg(nameof(value));
            Conv_I();
            return Return<IntPtr>();
        }

        /// <summary>
        /// Converts typed reference into managed pointer.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="reference">The typed reference.</param>
        /// <returns>A managed pointer to the value represented by reference.</returns>
        /// <exception cref="InvalidCastException"><typeparamref name="T"/> is not identical to the type stored in the typed reference.</exception>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AsRef<T>(this TypedReference reference)
        {
            Ldarg(nameof(reference));
            Refanyval(typeof(T));
            return ref ReturnRef<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref byte AddOffset<T>(this ref byte address, int count = 1)
        {
            Push(ref address);
            Push(count);
            Sizeof(typeof(T));
            Conv_I();
            Mul_Ovf();
            Add_Ovf();
            return ref ReturnRef<byte>();
        }

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Span<byte> AsSpan<T>(ref T value) where T : unmanaged => MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), sizeof(T));

        private static ReadOnlySpan<T> CreateReadOnlySpan<T>(in T address, int count = 1)
            => MemoryMarshal.CreateSpan<T>(ref Unsafe.AsRef(in address), count);

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> AsReadOnlySpan<T>(in T value) where T : unmanaged => MemoryMarshal.AsBytes(CreateReadOnlySpan<T>(in value, 1));

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="pointer">The typed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [CLSCompliant(false)]
        public static unsafe Span<byte> AsSpan<T>(T* pointer) where T : unmanaged => AsSpan(ref pointer[0]);

        internal static int Compare(ref byte first, ref byte second, long length)
        {
            var comparison = 0;
            for (int count; length > 0L && comparison == 0; length -= count, first = ref Unsafe.Add(ref first, count), second = ref Unsafe.Add(ref second, count))
            {
                count = length.Truncate();
                comparison = MemoryMarshal.CreateSpan(ref first, count).SequenceCompareTo(MemoryMarshal.CreateSpan(ref second, count));
            }
            return comparison;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T Read<T>(this ref byte address)
            where T : unmanaged
        {
            Push(ref address);
            Ldobj(typeof(T));
            return Return<T>();
        }

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="first">The pointer to the first memory block.</param>
        /// <param name="second">The pointer to the second memory block.</param>
        /// <param name="length">The length of the first and second memory blocks.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        [CLSCompliant(false)]
        public static unsafe int Compare(void* first, void* second, long length)
            => Compare(ref Unsafe.AsRef<byte>(first), ref Unsafe.AsRef<byte>(second), length);

        internal unsafe static bool EqualsAligned(ref byte first, ref byte second, long length)
        {
            var result = false;
            if (Vector.IsHardwareAccelerated)
                for (; length >= sizeof(Vector<byte>); first = ref first.AddOffset<Vector<byte>>(), second = ref second.AddOffset<Vector<byte>>())
                    if (first.Read<Vector<byte>>() == second.Read<Vector<byte>>())
                        length -= Vector<byte>.Count;
                    else
                        goto exit;
            for (; length >= sizeof(UIntPtr); first = ref first.AddOffset<UIntPtr>(), second = ref second.AddOffset<UIntPtr>())
                if (first.Read<UIntPtr>() == second.Read<UIntPtr>())
                    length -= sizeof(UIntPtr);
                else
                    goto exit;
            for (; length > 0; first = ref AddOffset<byte>(ref first), second = ref AddOffset<byte>(ref second))
                if (first == second)
                    length -= sizeof(byte);
                else
                    goto exit;
            result = true;
            exit:
            return result;
        }

        /// <summary>
		/// Computes equality between two blocks of memory.
		/// </summary>
		/// <param name="first">A pointer to the first memory block.</param>
		/// <param name="second">A pointer to the second memory block.</param>
		/// <param name="length">Length of first and second memory blocks, in bytes.</param>
		/// <returns><see langword="true"/>, if both memory blocks have the same data; otherwise, <see langword="false"/>.</returns>
		[CLSCompliant(false)]
        public static unsafe bool Equals(void* first, void* second, long length)
        {
            var result = true;
            for (int count; length > 0L && result; length -= count, first = Unsafe.Add<byte>(first, count), second = Unsafe.Add<byte>(first, count))
            {
                count = length.Truncate();
                result = new ReadOnlySpan<byte>(first, count).SequenceEqual(new ReadOnlySpan<byte>(second, count));
            }
            return result;
        }

        /// <summary>
        /// Gets a reference to the array element with restricted mutability.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array object.</param>
        /// <param name="index">The index of the array element.</param>
        /// <returns>The reference to the array element with restricted mutability.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T GetReadonlyRef<T>(this T[] array, long index)
        {
            Push(array);
            Push(index);
            Conv_Ovf_I();
            Readonly();
            Ldelema(typeof(T));
            return ref ReturnRef<T>();
        }

        /// <summary>
        /// Determines whether the specified managed pointer is <see langword="null"/>.
        /// </summary>
        /// <param name="value">The managed pointer to check.</param>
        /// <typeparam name="T">The type of the managed pointer.</typeparam>
        /// <returns><see langword="true"/>, if the specified managed pointer is <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNull<T>(in T value)
        {
            Ldarg(nameof(value));
            Ldnull();
            Conv_I();
            Ceq();
            return Return<bool>();
        }

        /// <summary>
        /// Throws <see cref="NullReferenceException"/> if given managed pointer is <see langword="null"/>.
        /// </summary>
        /// <param name="value">The managed pointer to check.</param>
        /// <typeparam name="T">The type of the managed pointer.</typeparam>
        /// <exception cref="NullReferenceException"><paramref name="value"/> pointer is <see langword="null"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull<T>(in T value)
        {
            Ldarg(nameof(value));
            Ldobj(typeof(T));
            Pop();
            Ret();
        }

        /// <summary>
        /// Copies one value into another.
        /// </summary>
        /// <typeparam name="T">The value type to copy.</typeparam>
        /// <param name="input">The reference to the source location.</param>
        /// <param name="output">The reference to the destination location.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(in T input, out T output)
            where T : struct
        {
            Ldarg(nameof(output));
            Ldarg(nameof(input));
            Cpobj(typeof(T));
            Ret();
            throw Unreachable();    //need here because output var should be assigned
        }

        /// <summary>
        /// Copies one value into another assuming unaligned memory access.
        /// </summary>
        /// <typeparam name="T">The value type to copy.</typeparam>
        /// <param name="input">The reference to the source location.</param>
        /// <param name="output">The reference to the destination location.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void CopyUnaligned<T>(T* input, T* output)
            where T : unmanaged
        {
            Push(output);
            Push(input);
            Unaligned(1); Ldobj(typeof(T));
            Unaligned(1); Stobj(typeof(T));
        }

        /// <summary>
        /// Copies one value into another.
        /// </summary>
        /// <typeparam name="T">The value type to copy.</typeparam>
        /// <param name="input">The reference to the source location.</param>
        /// <param name="output">The reference to the destination location.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void Copy<T>(T* input, T* output)
            where T : unmanaged
            => Copy(in input[0], out output[0]);

        private static void Copy(ref byte source, ref byte destination, long length)
        {
            for (int count; length > 0L; length -= count, source = ref Unsafe.Add(ref source, count), destination = ref Unsafe.Add(ref destination, count))
            {
                count = length.Truncate();
                Push(ref destination);
                Push(ref source);
                Push(count);
                Conv_Ovf_U4();
                Cpblk();
            }
        }

        /// <summary>
        /// Copies the specified number of elements from source address to the destination address.
        /// </summary>
        /// <param name="source">The address of the bytes to copy.</param>
        /// <param name="destination">The target address.</param>
        /// <param name="count">The number of elements to copy.</param>
        /// <typeparam name="T">The type of the element.</typeparam>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<T>(ref T source, ref T destination, long count)
            where T : unmanaged
            => Copy(ref Unsafe.As<T, byte>(ref source), ref Unsafe.As<T, byte>(ref destination), checked(count * sizeof(T)));

        /// <summary>
        /// Swaps two values.
        /// </summary>
        /// <param name="first">The first value to be replaced with <paramref name="second"/>.</param>
        /// <param name="second">The second value to be replaced with <paramref name="first"/>.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        public static void Swap<T>(ref T first, ref T second)
        {
            var tmp = first;
            first = second;
            second = tmp;
        }

        /// <summary>
        /// Swaps two values.
        /// </summary>
        /// <param name="first">The first value to be replaced with <paramref name="second"/>.</param>
        /// <param name="second">The second value to be replaced with <paramref name="first"/>.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Swap<T>(T* first, T* second)
            where T : unmanaged
            => Swap(ref first[0], ref second[0]);

        /// <summary>
        /// Indicates that two managed pointers are equal.
        /// </summary>
        /// <typeparam name="T">Type of managed pointer.</typeparam>
        /// <param name="first">The first managed pointer.</param>
        /// <param name="second">The second managed pointer.</param>
        /// <returns><see langword="true"/>, if both managed pointers are equal; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreSame<T>(in T first, in T second)
        {
            Ldarg(nameof(first));
            Ldarg(nameof(second));
            Ceq();
            return Return<bool>();
        }

        private static unsafe ref byte Advance<T>(this ref byte address, [In, Out]long* length)
            where T : unmanaged
        {
            Push(length);
            Dup();
            Ldind_I8();
            Sizeof(typeof(T));
            Conv_I8();
            Sub();
            Stind_I8();

            return ref address.AddOffset<T>();
        }

        private static unsafe bool IsZero(ref byte address, long length)
        {
            var result = false;
            if (Vector.IsHardwareAccelerated)
                while (length >= Vector<byte>.Count)
                    if (address.Read<Vector<byte>>() == Vector<byte>.Zero)
                        address = ref address.Advance<Vector<byte>>(&length);
                    else
                        goto exit;
            while (length >= sizeof(UIntPtr))
                if (address.Read<UIntPtr>() == default)
                    address = ref address.Advance<UIntPtr>(&length);
                else
                    goto exit;
            while (length > 0)
                if (address == 0)
                    address = ref address.Advance<byte>(&length);
                else
                    goto exit;
            result = true;
            exit:
            return result;
        }

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <param name="address">The pointer to the memory to be cleared.</param>
        /// <param name="length">The length of the memory to be cleared.</param>
        [CLSCompliant(false)]
        public static unsafe void ClearBits(void* address, long length)
        {
            for (int count; length > 0L; length -= count, address = Unsafe.Add<byte>(address, count))
            {
                count = length.Truncate();
                Push(address);
                Ldc_I4_0();
                Conv_U4();
                Push(count);
                Conv_U4();
                Initblk();
            }
        }

        #region Bitwise Hash Code

        internal static unsafe long GetHashCode64(ref byte source, long length, long hash, in ValueFunc<long, long, long> hashFunction, bool salted)
        {
            switch (length)
            {
                default:
                    for (; length >= sizeof(long); source = ref source.Advance<long>(&length))
                        hash = hashFunction.Invoke(hash, Unsafe.ReadUnaligned<long>(ref source));
                    for (; length > 0L; source = ref source.Advance<byte>(&length))
                        hash = hashFunction.Invoke(hash, source);
                    break;
                case sizeof(byte):
                    hash = hashFunction.Invoke(hash, source);
                    break;
                case sizeof(ushort):
                    hash = hashFunction.Invoke(hash, Unsafe.ReadUnaligned<ushort>(ref source));
                    break;
                case 3:
                    goto default;
                case sizeof(uint):
                    hash = hashFunction.Invoke(hash, Unsafe.ReadUnaligned<uint>(ref source));
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
            }
            return salted ? hashFunction.Invoke(hash, RandomExtensions.BitwiseHashSalt) : hash;
        }

        internal static unsafe long GetHashCode64(ref byte source, long length, bool salted)
        {
            var hash = FNV1a64.Offset;
            switch (length)
            {
                default:
                    for (; length >= sizeof(long); source = ref source.Advance<long>(&length))
                        hash = FNV1a64.GetHashCode(hash, Unsafe.ReadUnaligned<long>(ref source));
                    for (; length > 0L; source = ref source.Advance<byte>(&length))
                        hash = FNV1a64.GetHashCode(hash, source);
                    break;
                case sizeof(byte):
                    hash = FNV1a64.GetHashCode(hash, source);
                    break;
                case sizeof(ushort):
                    hash = FNV1a64.GetHashCode(hash, Unsafe.ReadUnaligned<ushort>(ref source));
                    break;
                case 3:
                    goto default;
                case sizeof(uint):
                    hash = FNV1a64.GetHashCode(hash, Unsafe.ReadUnaligned<uint>(ref source));
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
            }
            return salted ? FNV1a64.GetHashCode(hash, RandomExtensions.BitwiseHashSalt) : hash;
        }

        /// <summary>
		/// Computes 64-bit hash code for the block of memory, 64-bit version.
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
        public static unsafe long GetHashCode64(void* source, long length, long hash, Func<long, long, long> hashFunction, bool salted = true)
            => GetHashCode64(source, length, hash, new ValueFunc<long, long, long>(hashFunction, true), salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory, 64-bit version.
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
        public static unsafe long GetHashCode64(void* source, long length, long hash, in ValueFunc<long, long, long> hashFunction, bool salted = true)
            => GetHashCode64(ref ((byte*)source)[0], length, hash, in hashFunction, salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory.
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
        public static unsafe long GetHashCode64(void* source, long length, bool salted = true)
            => GetHashCode64(ref ((byte*)source)[0], length, salted);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
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
        public static unsafe int GetHashCode32(void* source, long length, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => GetHashCode32(source, length, hash, new ValueFunc<int, int, int>(hashFunction, true), salted);

        internal static unsafe int GetHashCode32(ref byte source, long length, int hash, in ValueFunc<int, int, int> hashFunction, bool salted)
        {
            switch (length)
            {
                default:
                    for (; length >= sizeof(int); source = ref source.Advance<int>(&length))
                        hash = hashFunction.Invoke(hash, Unsafe.ReadUnaligned<int>(ref source));
                    for (; length > 0L; source = ref source.Advance<byte>(&length))
                        hash = hashFunction.Invoke(hash, source);
                    break;
                case sizeof(byte):
                    hash = hashFunction.Invoke(hash, source);
                    break;
                case sizeof(ushort):
                    hash = hashFunction.Invoke(hash, Unsafe.ReadUnaligned<ushort>(ref source));
                    break;
            }
            return salted ? hashFunction.Invoke(hash, RandomExtensions.BitwiseHashSalt) : hash;
        }

        internal static unsafe int GetHashCode32(ref byte source, long length, bool salted)
        {
            var hash = FNV1a32.Offset;
            switch (length)
            {
                default:
                    for (; length >= sizeof(int); source = ref source.Advance<int>(&length))
                        hash = FNV1a32.GetHashCode(hash, Unsafe.ReadUnaligned<int>(ref source));
                    for (; length > 0L; source = ref source.Advance<byte>(&length))
                        hash = FNV1a32.GetHashCode(hash, source);
                    break;
                case sizeof(byte):
                    hash = FNV1a32.GetHashCode(hash, source);
                    break;
                case sizeof(ushort):
                    hash = FNV1a32.GetHashCode(hash, Unsafe.ReadUnaligned<ushort>(ref source));
                    break;
            }
            return salted ? FNV1a32.GetHashCode(hash, RandomExtensions.BitwiseHashSalt) : hash;
        }

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
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
        public static unsafe int GetHashCode32(void* source, long length, int hash, in ValueFunc<int, int, int> hashFunction, bool salted = true)
            => GetHashCode32(ref ((byte*)source)[0], length, hash, in hashFunction, salted);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
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
        public static unsafe int GetHashCode32(void* source, long length, bool salted = true)
            => GetHashCode32(ref ((byte*)source)[0], length, salted);
        #endregion

        /// <summary>
        /// Reverse bytes in the specified value of blittable type.
        /// </summary>
        /// <typeparam name="T">Blittable type.</typeparam>
        /// <param name="value">The value which bytes should be reversed.</param>
        public static void Reverse<T>(ref T value) where T : unmanaged => AsSpan(ref value).Reverse();
    }
}