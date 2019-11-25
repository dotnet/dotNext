using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using M = InlineIL.MethodRef;

namespace DotNext
{
    using Memory = Runtime.InteropServices.Memory;

    /// <summary>
    /// Represents bitwise comparer for the arbitrary value type.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public sealed class BitwiseComparer<T> : IEqualityComparer<T>, IComparer<T>
        where T : struct
    {
        private BitwiseComparer()
        {

        }

        /// <summary>
        /// Gets instance of this comparer.
        /// </summary>
        /// <remarks>
        /// Use this property only if you need object implementing <see cref="IEqualityComparer{T}"/>
        /// or <see cref="IComparer{T}"/> interface. Otherwise, use static methods.
        /// </remarks>
        /// <returns>The instance of this comparer.</returns>
        public static BitwiseComparer<T> Instance { get; } = new BitwiseComparer<T>();

        private static bool Equals<G>(ref T first, ref G second)
            where G : struct
        {
            const string methodExit = "exit";
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            Push(size);
            Sizeof(typeof(G));
            Conv_I8();
            Ceq();
            Dup();
            Brfalse(methodExit);
            Pop();
            switch (size)
            {
                default:
                    Push(ref first);
                    Push(ref second);
                    Push(size);
                    Call(new M(typeof(Memory), nameof(Memory.EqualsAligned)));
                    break;
                case sizeof(byte):
                    Push(ref first);
                    Ldind_U1();
                    Push(ref second);
                    Ldind_I1();
                    Ceq();
                    break;
                case sizeof(short):
                    Push(ref first);
                    Ldind_I2();
                    Push(ref second);
                    Ldind_I2();
                    Ceq();
                    break;
                case sizeof(int):
                    Push(ref first);
                    Ldind_I4();
                    Push(ref second);
                    Ldind_I4();
                    Ceq();
                    break;
                case sizeof(long):
                    Push(ref first);
                    Ldind_I8();
                    Push(ref second);
                    Ldind_I8();
                    Ceq();
                    break;
            }
            MarkLabel(methodExit);
            return Return<bool>();
        }

        /// <summary>
        /// Checks bitwise equality between two values of different value types.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.Equals(object)"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <typeparam name="G">Type of second value.</typeparam>
        /// <param name="first">The first value to check.</param>
        /// <param name="second">The second value to check.</param>
        /// <returns><see langword="true"/>, if both values are equal; otherwise, <see langword="false"/>.</returns>
        public static bool Equals<G>(T first, G second)
            where G : struct
            => Equals(ref first, ref second);

        private static int Compare<G>(ref T first, ref G second)
            where G : struct
        {
            const string methodExit = "exit";
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            Push(size);
            Sizeof(typeof(G));
            Conv_I8();
            Ceq();
            Dup();
            Brfalse(methodExit);
            Pop();
            switch (size)
            {
                default:
                    Push(ref first);
                    Push(ref second);
                    Push(size);
                    Call(new M(typeof(Memory), nameof(Memory.CompareUnaligned)));
                    break;
                case sizeof(byte):
                    Push(ref first);
                    Push(ref second);
                    Ldind_U1();
                    Call(new M(typeof(byte), nameof(byte.CompareTo), typeof(byte)));
                    break;
                case sizeof(ushort):
                    Push(ref first);
                    Push(ref second);
                    Ldind_U2();
                    Call(new M(typeof(ushort), nameof(ushort.CompareTo), typeof(ushort)));
                    break;
                case sizeof(uint):
                    Push(ref first);
                    Push(ref second);
                    Ldind_U4();
                    Call(new M(typeof(uint), nameof(uint.CompareTo), typeof(uint)));
                    break;
                case sizeof(ulong):
                    Push(ref first);
                    Push(ref second);
                    Ldobj(typeof(ulong));
                    Call(new M(typeof(ulong), nameof(ulong.CompareTo), typeof(ulong)));
                    break;
            }
            MarkLabel(methodExit);
            return Return<int>();
        }

        /// <summary>
        /// Compares bits of two values of the different type.
        /// </summary>
        /// <typeparam name="G">Type of the second value.</typeparam>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        public static int Compare<G>(T first, G second)
            where G : struct
            => Compare(ref first, ref second);

        private static int GetHashCode(ref T value, bool salted)
        {
            const string methodExit = "exit";
            Sizeof(typeof(T));
            Conv_I8();
            Pop(out long size);
            switch (size)
            {
                default:
                    Push(ref value);
                    Push(size);
                    Push(salted);
                    Call(new M(typeof(Memory), nameof(Memory.GetHashCode32Aligned), typeof(IntPtr), typeof(long), typeof(bool)));
                    return Return<int>();
                case sizeof(byte):
                    Push(ref value);
                    Ldind_I1();
                    break;
                case sizeof(short):
                    Push(ref value);
                    Ldind_I2();
                    break;
                case sizeof(int):
                    Push(ref value);
                    Ldind_I4();
                    break;
                case sizeof(long):
                    Push(ref value);
                    Call(new M(typeof(ulong), nameof(GetHashCode)));
                    break;
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
        public static int GetHashCode(T value, bool salted = true) => GetHashCode(ref value, salted);

        private static int GetHashCode(ref T value, int hash, in ValueFunc<int, int, int> hashFunction, bool salted)
        {
            Push(ref value);
            Sizeof(typeof(T));
            Conv_I8();
            Push(hash);
            Ldarg(nameof(hashFunction));
            Push(salted);
            Call(new M(typeof(Memory), nameof(Memory.GetHashCode32Aligned), typeof(IntPtr), typeof(long), typeof(int), typeof(ValueFunc<int, int, int>).MakeByRefType(), typeof(bool)));
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(T value, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => GetHashCode(ref value, hash, new ValueFunc<int, int, int>(hashFunction, true), salted);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(T value, int hash, in ValueFunc<int, int, int> hashFunction, bool salted = true)
            => GetHashCode(ref value, hash, hashFunction, salted);

        bool IEqualityComparer<T>.Equals(T x, T y) => Equals(ref x, ref y);

        int IEqualityComparer<T>.GetHashCode(T obj) => GetHashCode(ref obj, true);

        int IComparer<T>.Compare(T x, T y) => Compare(ref x, ref y);
    }
}