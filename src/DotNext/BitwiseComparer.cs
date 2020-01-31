using System;
using System.Collections.Generic;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using M = InlineIL.MethodRef;
using TR = InlineIL.TypeRef;

namespace DotNext
{
    using Intrinsics = Runtime.Intrinsics;

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
        public static bool Equals<G>(in T first, in G second)
            where G : struct
        {
            const string methodExit = "exit";
            Sizeof(typeof(T));
            Pop(out uint size);
            Push(size);
            Sizeof(typeof(G));
            Ceq();
            Dup();
            Brfalse(methodExit);
            Pop();
            switch (size)
            {
                default:
                    Ldarg(nameof(first));
                    Ldarg(nameof(second));
                    Push(size);
                    Conv_I8();
                    Call(new M(typeof(Intrinsics), nameof(Intrinsics.EqualsAligned)));
                    break;
                case 0U:
                    Ldc_I4_1();
                    break;
                case sizeof(byte):
                    Ldarg(nameof(first));
                    Ldind_U1();
                    Ldarg(nameof(second));
                    Ldind_I1();
                    Ceq();
                    break;
                case sizeof(short):
                    Ldarg(nameof(first));
                    Ldind_I2();
                    Ldarg(nameof(second));
                    Ldind_I2();
                    Ceq();
                    break;
                case 3:
                    goto default;
                case sizeof(int):
                    Ldarg(nameof(first));
                    Ldind_I4();
                    Ldarg(nameof(second));
                    Ldind_I4();
                    Ceq();
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
                case sizeof(long):
                    Ldarg(nameof(first));
                    Ldind_I8();
                    Ldarg(nameof(second));
                    Ldind_I8();
                    Ceq();
                    break;
            }
            MarkLabel(methodExit);
            return Return<bool>();
        }

        /// <summary>
        /// Compares bits of two values of the different type.
        /// </summary>
        /// <typeparam name="G">Type of the second value.</typeparam>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        public static int Compare<G>(in T first, in G second)
            where G : struct
        {
            const string methodExit = "exit";
            Sizeof(typeof(T));
            Pop(out uint size);
            Push(size);
            Sizeof(typeof(G));
            Ceq();
            Dup();
            Brfalse(methodExit);
            Pop();
            switch (size)
            {
                default:
                    Ldarg(nameof(first));
                    Ldarg(nameof(second));
                    Push(size);
                    Conv_I8();
                    Call(new M(typeof(Intrinsics), nameof(Intrinsics.Compare), new TR(typeof(byte)).MakeByRefType(), new TR(typeof(byte)).MakeByRefType(), typeof(long)));
                    break;
                case 0U:
                    Ldc_I4_0();
                    break;
                case sizeof(byte):
                    Ldarg(nameof(first));
                    Ldarg(nameof(second));
                    Ldind_U1();
                    Call(new M(typeof(byte), nameof(byte.CompareTo), typeof(byte)));
                    break;
                case sizeof(ushort):
                    Ldarg(nameof(first));
                    Ldarg(nameof(second));
                    Ldind_U2();
                    Call(new M(typeof(ushort), nameof(ushort.CompareTo), typeof(ushort)));
                    break;
                case 3:
                    goto default;
                case sizeof(uint):
                    Ldarg(nameof(first));
                    Ldarg(nameof(second));
                    Ldind_U4();
                    Call(new M(typeof(uint), nameof(uint.CompareTo), typeof(uint)));
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
                case sizeof(ulong):
                    Ldarg(nameof(first));
                    Ldarg(nameof(second));
                    Ldobj(typeof(ulong));
                    Call(new M(typeof(ulong), nameof(ulong.CompareTo), typeof(ulong)));
                    break;
            }
            MarkLabel(methodExit);
            return Return<int>();
        }

        /// <summary>
        /// Computes hash code for the structure content.
        /// </summary>
        /// <param name="value">Value to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public static int GetHashCode(in T value, bool salted = true)
        {
            const string methodExit = "exit";
            Sizeof(typeof(T));
            Pop(out uint size);
            switch (size)
            {
                default:
                    Ldarg(nameof(value));
                    Push(size);
                    Conv_I8();
                    Push(salted);
                    Call(new M(typeof(Intrinsics), nameof(Intrinsics.GetHashCode32), new TR(typeof(byte)).MakeByRefType(), typeof(long), typeof(bool)));
                    return Return<int>();
                case 0U:
                    Ldc_I4_0();
                    break;
                case sizeof(byte):
                    Ldarg(nameof(value));
                    Ldind_I1();
                    break;
                case sizeof(short):
                    Ldarg(nameof(value));
                    Ldind_I2();
                    break;
                case 3:
                    goto default;
                case sizeof(int):
                    Ldarg(nameof(value));
                    Ldind_I4();
                    break;
                case 5:
                case 6:
                case 7:
                    goto default;
                case sizeof(long):
                    Ldarg(nameof(value));
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
        public static int GetHashCode(in T value, int hash, in ValueFunc<int, int, int> hashFunction, bool salted)
        {
            Ldarg(nameof(value));
            Sizeof(typeof(T));
            Conv_I8();
            Push(hash);
            Ldarg(nameof(hashFunction));
            Push(salted);
            Call(new M(typeof(Intrinsics), nameof(Intrinsics.GetHashCode32), new TR(typeof(byte)).MakeByRefType(), typeof(long), typeof(int), typeof(ValueFunc<int, int, int>).MakeByRefType(), typeof(bool)));
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
        public static int GetHashCode(in T value, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => GetHashCode(in value, hash, new ValueFunc<int, int, int>(hashFunction, true), salted);


        bool IEqualityComparer<T>.Equals(T x, T y) => Equals(in x, in y);

        int IEqualityComparer<T>.GetHashCode(T obj) => GetHashCode(in obj, true);

        int IComparer<T>.Compare(T x, T y) => Compare(in x, in y);
    }
}