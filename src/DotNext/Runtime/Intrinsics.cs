using System;
using System.Collections;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using Debug = System.Diagnostics.Debug;
using M = InlineIL.MethodRef;
using Var = InlineIL.LocalVar;

namespace DotNext.Runtime
{
    using Memory = InteropServices.Memory;

    /// <summary>
    /// Represents highly optimized runtime intrinsic methods.
    /// </summary>
    public static class Intrinsics
    {
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
            where T : struct, IStructuralEquatable, IStructuralComparable
        {
            //TODO: Should be rewritten with ITuple interface in .NET Standard 2.1
            Sizeof(typeof(T));
            Sizeof(typeof(E));
            Div_Un();
            Pop(out int count);
            if (index < 0 || index >= count)
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
    }
}