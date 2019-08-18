using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using Var = InlineIL.LocalVar;
using M = InlineIL.MethodRef;

namespace DotNext.Runtime
{
    using Memory = InteropServices.Memory;

    /// <summary>
    /// Represents highly optimized runtime intrinsic methods.
    /// </summary>
    public static class Intrinsics
    {
        /// <summary>
        /// Represents bitwise comparer for value type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        public sealed class BitwiseComparer<T> : IEqualityComparer<T>, IComparer<T>
            where T : struct
        {
            internal static readonly BitwiseComparer<T> Instance = new BitwiseComparer<T>();

            private BitwiseComparer()
            {
            }

            /// <summary>
            /// Determines whether two values are equal based on bitwise equality check.
            /// </summary>
            /// <param name="first">The first value to be compared.</param>
            /// <param name="second">The second value to be compared.</param>
            /// <returns><see langword="true"/>, if two values are equal; otherwise, <see langword="false"/>.</returns>
            /// <seealso cref="BitwiseEquals{T1, T2}(T1, T2)"/>
            public bool Equals(T first, T second)
                => BitwiseEquals(ref first, ref second);

            /// <summary>
            /// Computes bitwise hash code for the given value.
            /// </summary>
            /// <param name="obj">The value for which a hash code is to be returned.</param>
            /// <returns>A hash code for the specified object.</returns>
            /// <seealso cref="BitwiseHashCode{T}(T, bool)"/>
            public int GetHashCode(T obj) => BitwiseHashCode(ref obj, true);

            /// <summary>
            /// Performs bitwise comparison between two values.
            /// </summary>
            /// <param name="first">The first value to compare.</param>
            /// <param name="second">The second value to compare.</param>
            /// <returns>A value that indicates the relative order of the objects being compared.</returns>
            /// <seealso cref="BitwiseCompare{T1, T2}(T1, T2)"/>
            public int Compare(T first, T second) => BitwiseCompare(ref first, ref second);
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
        /// and able to provide conversion between types of different size.
        /// </remarks>
        /// <param name="input">A value to convert.</param>
        /// <param name="output">Conversion result.</param>
        /// <typeparam name="T">The value type to be converted.</typeparam>
        /// <typeparam name="TResult">The type of output struct.</typeparam>
        public static void Bitcast<T, TResult>(in T input, out TResult output)
            where T : struct
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
                    Call(new M(typeof(Memory), nameof(Memory.IsZeroAligned)));
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

        private static bool BitwiseEquals<T1, T2>(ref T1 first, ref T2 second)
            where T1 : struct
            where T2 : struct
        {
            const string methodExit = "exit";
            Sizeof(typeof(T1));
            Conv_I8();
            Pop(out long size);
            Push(size);
            Sizeof(typeof(T2));
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
        /// <typeparam name="T1">Type of first value.</typeparam>
        /// <typeparam name="T2">Type of second value.</typeparam>
        /// <param name="first">The first value to check.</param>
        /// <param name="second">The second value to check.</param>
        /// <returns><see langword="true"/>, if both values are equal; otherwise, <see langword="false"/>.</returns>
        public static bool BitwiseEquals<T1, T2>(T1 first, T2 second)
            where T1 : struct
            where T2 : struct
            => BitwiseEquals(ref first, ref second);

        private static int BitwiseCompare<T1, T2>(ref T1 first, ref T2 second)
            where T1 : struct
            where T2 : struct
        {
            const string methodExit = "exit";
            Sizeof(typeof(T1));
            Conv_I8();
            Pop(out long size);
            Push(size);
            Sizeof(typeof(T2));
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
        /// <typeparam name="T1">Type of the first value.</typeparam>
        /// <typeparam name="T2">Type of the second value.</typeparam>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        public static int BitwiseCompare<T1, T2>(T1 first, T2 second)
            where T1 : struct
            where T2 : struct
            => BitwiseCompare(ref first, ref second);

        private static int BitwiseHashCode<T>(ref T value, bool salted) 
            where T : struct
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
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">Value to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public static int BitwiseHashCode<T>(T value, bool salted = true) where T : struct => BitwiseHashCode(ref value, salted);

        private static int BitwiseHashCode<T>(ref T value, int hash, in ValueFunc<int, int, int> hashFunction, bool salted)
            where T : struct
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
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">A value to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Bitwise hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseHashCode<T>(T value, int hash, Func<int, int, int> hashFunction, bool salted = true)
            where T : struct
            => BitwiseHashCode(ref value, hash, new ValueFunc<int, int, int>(hashFunction, true), salted);

        /// <summary>
        /// Computes bitwise hash code for the specified value.
        /// </summary>
        /// <remarks>
        /// This method doesn't use <see cref="object.GetHashCode"/>
        /// even if it is overridden by value type.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">A value to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Bitwise hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitwiseHashCode<T>(T value, int hash, in ValueFunc<int, int, int> hashFunction, bool salted = true)
            where T : struct
            => BitwiseHashCode(ref value, hash, hashFunction, salted);

        /// <summary>
        /// Gets bitwise comparer for the specified value type.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The bitwise comparer for the specified value type.</returns>
        public static BitwiseComparer<T> GetBitwiseComparer<T>() where T : struct => BitwiseComparer<T>.Instance;
    }
}