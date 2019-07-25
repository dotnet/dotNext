using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using M = InlineIL.MethodRef;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext
{
    using Runtime.InteropServices;

    /// <summary>
    /// Provides fast memory operations to work with value type.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
	[Serializable]
    public sealed class ValueType<T> : StrongBox<T>
        where T : struct
    {
        /// <summary>
        /// Represents bitwise comparer for value type <typeparamref name="T"/>.
        /// </summary>
        public sealed class BitwiseComparer : IEqualityComparer<T>, IComparer<T>
        {
            /// <summary>
            /// Represents singleton instance of bitwise comparer.
            /// </summary>
            public static readonly BitwiseComparer Instance = new BitwiseComparer();

            private BitwiseComparer()
            {
            }

            /// <summary>
            /// Determines whether two values are equal based on bitwise equality check.
            /// </summary>
            /// <param name="first">The first value to be compared.</param>
            /// <param name="second">The second value to be compared.</param>
            /// <returns><see langword="true"/>, if two values are equal; otherwise, <see langword="false"/>.</returns>
            /// <seealso cref="BitwiseEquals(T, T)"/>
            public bool Equals(T first, T second)
                => BitwiseEquals(ref Unsafe.As<T, byte>(ref first), ref Unsafe.As<T, byte>(ref second));

            /// <summary>
            /// Computes bitwise hash code for the given value.
            /// </summary>
            /// <param name="obj">The value for which a hash code is to be returned.</param>
            /// <returns>A hash code for the specified object.</returns>
            /// <seealso cref="BitwiseHashCode(T)"/>
            public int GetHashCode(T obj) => BitwiseHashCode(ref obj, true);

            /// <summary>
            /// Performs bitwise comparison between two values.
            /// </summary>
            /// <param name="first">The first value to compare.</param>
            /// <param name="second">The second value to compare.</param>
            /// <returns>A value that indicates the relative order of the objects being compared.</returns>
            /// <seealso cref="BitwiseCompare(T, T)"/>
            public int Compare(T first, T second) => BitwiseCompare(ref As<T, byte>(ref first), ref As<T, byte>(ref second));
        }

        /// <summary>
        /// Size of value type, in bytes.
        /// </summary>
        public static int Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SizeOf<T>();
        }

        /// <summary>
        /// Default value of type <typeparamref name="T"/>.
        /// </summary>
        public static T Default => default;

        /// <summary>
        /// Indicates that value type is primitive type.
        /// </summary>
        public static readonly bool IsPrimitive = typeof(T).IsPrimitive;

        private static bool BitwiseEquals(ref byte first, ref byte second)
        {
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            switch (size)
            {
                case 1:
                    Push(ref first);
                    Ldind_I1();
                    Push(ref second);
                    Ldind_I1();
                    Ceq();
                    break;
                case 2:
                    Push(ref first);
                    Ldind_I2();
                    Push(ref second);
                    Ldind_I2();
                    Ceq();
                    break;
                case 3:
                    goto default;
                case 4:
                    Push(ref first);
                    Ldind_I4();
                    Push(ref second);
                    Ldind_I4();
                    Ceq();
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
                case 8:
                    Push(ref first);
                    Ldind_I8();
                    Push(ref second);
                    Ldind_I8();
                    Ceq();
                    break;
                default:
                    Push(ref first);
                    Push(ref second);
                    Push(size);
                    Call(new M(typeof(Memory), nameof(Memory.EqualsAligned)));
                    break;
            }
            return Return<bool>();
        }

        /// <summary>
        /// Checks bitwise equality between two values of different value types.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.Equals(object)"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <typeparam name="U">Type of second value.</typeparam>
        /// <param name="first">The first value to check.</param>
        /// <param name="second">The second value to check.</param>
        /// <returns><see langword="true"/>, if both values are equal; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BitwiseEquals<U>(T first, U second)
            where U : struct
            => SizeOf<T>() == SizeOf<U>() && BitwiseEquals(ref As<T, byte>(ref first), ref As<U, byte>(ref second));

        /// <summary>
        /// Checks bitwise equality between two values of the same value type.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.Equals(object)"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <param name="first">The first value to check.</param>
        /// <param name="second">The second value to check.</param>
        /// <returns><see langword="true"/>, if both values are equal; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BitwiseEquals(T first, T second) 
            => BitwiseEquals(ref As<T, byte>(ref first), ref As<T, byte>(ref second));

        /// <summary>
        /// Checks bitwise equality between two values of the same value type.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.Equals(object)"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <param name="first">The first value to check.</param>
        /// <param name="second">The second value to check.</param>
        /// <returns><see langword="true"/>, if both values are equal; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BitwiseEquals(in StackLocal<T> first, in StackLocal<T> second)
            => BitwiseEquals(ref As<T, byte>(ref first.Ref), ref As<T, byte>(ref second.Ref));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BitwiseHashCode(ref T value, int hash, FunctionPointer<int, int, int> hashFunction, bool salted)
        {
            Push(ref value);
            Sizeof(typeof(T));
            Conv_I8();
            Push(hash);
            Push(hashFunction);
            Push(salted);
            Call(new M(typeof(Memory), nameof(Memory.GetHashCode32Aligned), typeof(IntPtr), typeof(long), typeof(int), typeof(FunctionPointer<int, int, int>), typeof(bool)));
            return Return<int>();
        }

        /// <summary>
        /// Computes bitwise hash code for the specified value.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.GetHashCode"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <param name="value">A value to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Bitwise hash code.</returns>
        public static int BitwiseHashCode(T value, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => BitwiseHashCode(ref value, hash, new FunctionPointer<int, int, int>(hashFunction), salted);

        /// <summary>
        /// Computes bitwise hash code for the specified value.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.GetHashCode"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <param name="value">A value to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Bitwise hash code.</returns>
        public static int BitwiseHashCode(T value, int hash, FunctionPointer<int, int, int> hashFunction, bool salted = true)
            => BitwiseHashCode(ref value, hash, hashFunction, salted);

        /// <summary>
        /// Computes bitwise hash code for the specified value.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.GetHashCode"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <param name="value">Stack-allocated value to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Bitwise hash code.</returns>
        public static int BitwiseHashCode(in StackLocal<T> value, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => BitwiseHashCode(ref value.Ref, hash, new FunctionPointer<int, int, int>(hashFunction), salted);

        /// <summary>
        /// Computes bitwise hash code for the specified value.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.GetHashCode"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <param name="value">Stack-allocated value to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Bitwise hash code.</returns>
        public static int BitwiseHashCode(in StackLocal<T> value, int hash, FunctionPointer<int, int, int> hashFunction, bool salted = true)
            => BitwiseHashCode(ref value.Ref, hash, hashFunction, salted);

        private static int BitwiseHashCode(ref T value, bool salted)
        {
            const string methodExit = "exit";
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            switch(size)
            {
                case 1:
                    Push(ref value);
                    Ldind_I1();
                    break;
                case 2:
                    Push(ref value);
                    Ldind_I2();
                    break;
                case 3:
                    goto default;
                case 4:
                    Push(ref value);
                    Ldind_I4();
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
                case 8:
                    Push(ref value);
                    Call(new M(typeof(ulong), nameof(GetHashCode)));
                    break;
                default:
                    Push(ref value);
                    Push(size);
                    Push(salted);
                    Call(new M(typeof(Memory), nameof(Memory.GetHashCode32Aligned), typeof(IntPtr), typeof(long), typeof(bool)));
                    return Return<int>();
            }
            Push(salted);
            Brfalse(methodExit);
            Push(RandomExtensions.BitwiseHashSalt);
            Add();
            MarkLabel(methodExit);
            return Return<int>();
        }

        /// <summary>
        /// Computes hash code for the structure content.
        /// </summary>
        /// <param name="value">Value to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseHashCode(T value, bool salted) => BitwiseHashCode(ref value, salted);

        /// <summary>
		/// Computes salted hash code for the structure content.
		/// </summary>
		/// <param name="value">Value to be hashed.</param>
		/// <returns>Content hash code.</returns>   
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseHashCode(T value) => BitwiseHashCode(ref value, true);

        /// <summary>
        /// Computes hash code for the structure content.
        /// </summary>
        /// <param name="value">Stack-allocated value to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseHashCode(in StackLocal<T> value, bool salted) => BitwiseHashCode(ref value.Ref, salted);

        /// <summary>
		/// Computes salted hash code for the structure content.
		/// </summary>
		/// <param name="value">Stack-allocated value to be hashed.</param>
		/// <returns>Content hash code.</returns>   
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseHashCode(in StackLocal<T> value) => BitwiseHashCode(value, true);

        private static bool IsDefault(ref T value)
        {
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            switch (size)
            {
                case 1:
                    Push(ref value);
                    Ldind_I1();
                    Ldc_I4_0();
                    Ceq();
                    break;
                case 2:
                    Push(ref value);
                    Ldind_I2();
                    Ldc_I4_0();
                    Ceq();
                    break;
                case 3:
                    goto default;
                case 4:
                    Push(ref value);
                    Ldind_I4();
                    Ldc_I4_0();
                    Ceq();
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
                case 8:
                    Push(ref value);
                    Ldind_I8();
                    Ldc_I8(0L);
                    Ceq();
                    break;
                default:
                    Push(ref value);
                    Push(size);
                    Call(new M(typeof(Memory), nameof(Memory.IsZeroAligned)));
                    break;
            }
            return Return<bool>();
        }

        /// <summary>
        /// Indicates that specified value type is the default value.
        /// </summary>
        /// <param name="value">Value to check.</param>
        /// <returns><see langword="true"/>, if value is default value; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDefault(T value) => IsDefault(ref value);

        /// <summary>
        /// Indicates that specified value type is the default value.
        /// </summary>
        /// <param name="value">Value to check.</param>
        /// <returns><see langword="true"/>, if value is default value; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDefault(in StackLocal<T> value) => IsDefault(ref value.Ref);

        /// <summary>
        /// Convert value type content into array of bytes.
        /// </summary>
        /// <param name="value">Stack-allocated value to convert.</param>
        /// <returns>An array of bytes representing binary content of value type.</returns>
        public static unsafe byte[] AsBinary(T value)
            => new ReadOnlySpan<byte>(AsPointer(ref value), Size).ToArray();

        private static int BitwiseCompare(ref byte first, ref byte second)
        {
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            switch(size)
            {
                case 1:
                    Push(ref first);
                    Push(ref second);
                    Ldind_U1();
                    Call(new M(typeof(byte), nameof(byte.CompareTo), typeof(byte)));
                    break;
                case 2:
                    Push(ref first);
                    Push(ref second);
                    Ldind_U2();
                    Call(new M(typeof(ushort), nameof(ushort.CompareTo), typeof(ushort)));
                    break;
                case 3:
                    goto default;
                case 4:
                    Push(ref first);
                    Push(ref second);
                    Ldind_U4();
                    Call(new M(typeof(uint), nameof(uint.CompareTo), typeof(uint)));
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
                case 8:
                    Push(ref first);
                    Push(ref second);
                    Ldobj(typeof(ulong));
                    Call(new M(typeof(ulong), nameof(ulong.CompareTo), typeof(ulong)));
                    break;
                default:
                    Push(ref first);
                    Push(ref second);
                    Push(size);
                    Call(new M(typeof(Memory), nameof(Memory.CompareUnaligned)));
                    break;
            }
            return Return<int>();
        }

        /// <summary>
        /// Compares bits of two values of the same type.
        /// </summary>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseCompare(T first, T second) => BitwiseCompare(ref As<T, byte>(ref first), ref As<T, byte>(ref second));

        /// <summary>
        /// Compares bits of two values of the same type.
        /// </summary>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseCompare(in StackLocal<T> first, in StackLocal<T> second)
            => BitwiseCompare(ref As<T, byte>(ref first.Ref), ref As<T, byte>(ref second.Ref));

        /// <summary>
        /// Compares bits of two values of the different type.
        /// </summary>
        /// <typeparam name="U">Type of the second value.</typeparam>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseCompare<U>(T first, U second)
            where U : struct
            => SizeOf<T>() == SizeOf<U>() ? BitwiseCompare(ref As<T, byte>(ref first), ref As<U, byte>(ref second)) : SizeOf<T>().CompareTo(SizeOf<U>());
            

        /// <summary>
        /// Obtain a value of type <typeparamref name="To"/> by 
        /// reinterpreting the object representation of <typeparamref name="T"/>. 
        /// </summary>
        /// <remarks>
        /// Every bit in the value representation of the returned <typeparamref name="To"/> object 
        /// is equal to the corresponding bit in the object representation of <typeparamref name="T"/>. 
        /// The values of padding bits in the returned <typeparamref name="To"/> object are unspecified. 
        /// The method takes into account size of <typeparamref name="T"/> and <typeparamref name="To"/> types
        /// and able to provide conversion between types of different size.
        /// </remarks>
        /// <param name="input">A value to convert.</param>
        /// <param name="output">Conversion result.</param>
        /// <typeparam name="To">The type of output struct.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Bitcast<To>(in T input, out To output)
            where To : unmanaged
        {
            if (Size >= ValueType<To>.Size)
                output = Unsafe.As<T, To>(ref Unsafe.AsRef(in input));
            else
            {
                output = default;
                Unsafe.As<To, T>(ref output) = input;
            }
        }

        /// <summary>
        /// Attempts to unbox value type.
        /// </summary>
        /// <param name="boxed">The boxed struct.</param>
        /// <returns>Unboxed representation of <typeparamref name="T"/>.</returns>
        public static T? TryUnbox(object boxed)
        {
            switch (boxed)
            {
                case T vt:
                    return vt;
                case Optional<T> optional:
                    return optional.OrNull();
                case Result<T> result:
                    return result.OrNull();
                case ValueType<T> vt:
                    return vt.Value;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Initializes a new boxed value type.
        /// </summary>
        /// <param name="value">A struct to be placed onto heap.</param>
        public ValueType(T value)
            : base(value)
        {
        }

        /// <summary>
        /// Gets pinnable reference to the boxed value.
        /// </summary>
        /// <returns>Pinnable reference.</returns>
        public ref T GetPinnableReference() => ref Value;

        /// <summary>
        /// Unbox value type.
        /// </summary>
        /// <param name="box">Boxed representation of value type to unbox.</param>
        public static implicit operator T(ValueType<T> box) => box.Value;
    }
}