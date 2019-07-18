using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using M = InlineIL.MethodRef;

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
            public bool Equals(T first, T second) => BitwiseEquals(first, second);

            /// <summary>
            /// Computes bitwise hash code for the given value.
            /// </summary>
            /// <param name="obj">The value for which a hash code is to be returned.</param>
            /// <returns>A hash code for the specified object.</returns>
            /// <seealso cref="BitwiseHashCode(T)"/>
            public int GetHashCode(T obj) => BitwiseHashCode(obj);

            /// <summary>
            /// Performs bitwise comparison between two values.
            /// </summary>
            /// <param name="first">The first value to compare.</param>
            /// <param name="second">The second value to compare.</param>
            /// <returns>A value that indicates the relative order of the objects being compared.</returns>
            /// <seealso cref="BitwiseCompare(T, T)"/>
            public int Compare(T first, T second) => BitwiseCompare(first, second);
        }

        /// <summary>
        /// Size of value type, in bytes.
        /// </summary>
        public static int Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// Default value of type <typeparamref name="T"/>.
        /// </summary>
        public static T Default => default;

        /// <summary>
        /// Indicates that value type is primitive type.
        /// </summary>
        public static readonly bool IsPrimitive = typeof(T).IsPrimitive;

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
        public static bool BitwiseEquals<U>(T first, U second)
            where U : struct
        {
            const string methodExit = "exit";
            const string fastPath = "fastPath";
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            Push(size);
            Sizeof(typeof(U));
            Conv_I8();
            Ceq();
            Dup();
            Brfalse(methodExit);//sizeof(T) != sizeof(U), return false

            Pop();  //to remove value produced by Dup()
            Push(size);
            Ldc_I8(sizeof(long));
            Ble(fastPath);
            //size > sizeof(ulong)
            Ldarga(0);
            Ldarga(1);
            Push(size);
            Call(new M(typeof(Memory), nameof(Memory.EqualsAligned)));
            Br(methodExit);
            //size <= sizeof(ulong), just compare two values
            MarkLabel(fastPath);
            Push(first);
            Push(second);
            Ceq();

            MarkLabel(methodExit);
            return Return<bool>();
        }

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
        public static bool BitwiseEquals(T first, T second)
        {
            //cannot just call BitwiseEquals<U> because of performance issues measured during BitwiseEqualityBenchmark
            //probably this problem caused by double passing of large structs through stack
            const string methodExit = "exit";
            const string fastPath = "fastPath";
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            Push(size);
            Ldc_I8(sizeof(long));
            Ble(fastPath);
            //size > sizeof(ulong)
            Ldarga(0);
            Ldarga(1);
            Push(size);
            Call(new M(typeof(Memory), nameof(Memory.EqualsAligned)));
            Br(methodExit);
            //size <= sizeof(ulong), just compare two values
            MarkLabel(fastPath);
            Push(first);
            Push(second);
            Ceq();

            MarkLabel(methodExit);
            return Return<bool>();
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
        public static unsafe int BitwiseHashCode(T value, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => Memory.GetHashCode32(Unsafe.AsPointer(ref value), Size, hash, hashFunction, salted);

        /// <summary>
        /// Computes hash code for the structure content.
        /// </summary>
        /// <param name="value">Value to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int BitwiseHashCode(T value, bool salted)
            => Memory.GetHashCode32(Unsafe.AsPointer(ref value), Size, salted);

        /// <summary>
		/// Computes salted hash code for the structure content.
		/// </summary>
		/// <param name="value">Value to be hashed.</param>
		/// <returns>Content hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]      
        public static unsafe int BitwiseHashCode(T value) => Memory.GetHashCode32(Unsafe.AsPointer(ref value), Size, true);

        /// <summary>
        /// Indicates that specified value type is the default value.
        /// </summary>
        /// <param name="value">Value to check.</param>
        /// <returns><see langword="true"/>, if value is default value; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefault(T value)
        {
            const string methodExit = "exit";
            const string fastPath = "fastPath";
            Sizeof(typeof(T));
            Conv_I4();
            Pop(out int size);
            Push(size);
            Ldc_I4_8(); //sizeof(ulong) is 8 bytes

            Ble(fastPath);  //size <= sizeof(ulong), move to fast path
            //size < sizeof(ulong)
            Ldarga(0);
            Push(size);
            Call(new M(typeof(Memory), nameof(Memory.IsZeroAligned)));
            Br(methodExit);
            //size <= sizeof(ulong)
            MarkLabel(fastPath);
            Push(value);
            Conv_I8();
            Ldc_I8(0L);
            Ceq();
            MarkLabel(methodExit);
            return Return<bool>();
        }

        /// <summary>
        /// Convert value type content into array of bytes.
        /// </summary>
        /// <param name="value">A value to convert.</param>
        /// <returns>An array of bytes representing binary content of value type.</returns>
        public static unsafe byte[] AsBinary(T value)
            => new ReadOnlySpan<byte>(Unsafe.AsPointer(ref value), Size).ToArray();

        /// <summary>
        /// Compares bits of two values of the same type.
        /// </summary>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        public static unsafe int BitwiseCompare(T first, T second)
            => Memory.Compare(Unsafe.AsPointer(ref first), Unsafe.AsPointer(ref second), Size);

        /// <summary>
        /// Compares bits of two values of the different type.
        /// </summary>
        /// <typeparam name="U">Type of the second value.</typeparam>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        public static unsafe int BitwiseCompare<U>(T first, U second)
            where U : struct
            => Size == ValueType<U>.Size ?
                    Memory.Compare(Unsafe.AsPointer(ref first), Unsafe.AsPointer(ref second), Size) :
                    Size.CompareTo(ValueType<U>.Size);

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