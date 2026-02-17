using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Runtime.CompilerServices;

partial class AdvancedHelpers
{
    /// <summary>
    /// Extends <see cref="RuntimeHelpers"/>
    /// </summary>
    extension(RuntimeHelpers)
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
        public static bool IsNullable<T>() => default(T) is null;
        
        /// <summary>
        /// Indicates that specified value type is the default value.
        /// </summary>
        /// <typeparam name="T">The type of the value to check.</typeparam>
        /// <param name="value">Value to check.</param>
        /// <returns><see langword="true"/>, if value is default value; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefault<T>(in T value) => Unsafe.SizeOf<T>() switch
        {
            0 => true,
            sizeof(byte) => Unsafe.InToRef<T, byte>(in value) is 0,
            sizeof(ushort) => Unsafe.ReadUnaligned<ushort>(ref InToRef<T, byte>(in value)) is 0,
            sizeof(uint) => Unsafe.ReadUnaligned<uint>(ref InToRef<T, byte>(in value)) is 0U,
            sizeof(ulong) => Unsafe.ReadUnaligned<ulong>(ref InToRef<T, byte>(in value)) is 0UL,
            _ => IsZero(ref Unsafe.InToRef<T, byte>(in value), (nuint)Unsafe.SizeOf<T>()),
        };
        
        /// <summary>
        /// Copies one value into another.
        /// </summary>
        /// <typeparam name="T">The value type to copy.</typeparam>
        /// <param name="input">The reference to the source location.</param>
        /// <param name="output">The reference to the destination location.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(in T input, out T output)
            where T : struct, allows ref struct
        {
            PushOutRef(out output);
            PushInRef(in input);
            Cpobj<T>();
            Ret();
        }

        /// <summary>
        /// Swaps two values.
        /// </summary>
        /// <param name="first">The first value to be replaced with <paramref name="second"/>.</param>
        /// <param name="second">The second value to be replaced with <paramref name="first"/>.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        public static void Swap<T>(ref T first, ref T second)
            where T : allows ref struct
        {
            var tmp = first;
            first = second;
            second = tmp;
        }

        /// <summary>
        /// Determines whether the object overrides <see cref="object.Finalize()"/> method.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns><see langword="true"/> if <see cref="object.Finalize()"/> is overridden; otherwise, <see langword="false"/>.</returns>
        public static bool HasFinalizer(object obj)
        {
            Push(obj);
            Ldvirtftn(Method(Type<object>(), nameof(Finalize)));
            Ldftn(Method(Type<object>(), nameof(Finalize)));
            Ceq();
            Ldc_I4_0();
            Ceq();
            return Return<bool>();
        }
    }

    /// <summary>
    /// Provides static extensions for the arbitrary type.
    /// </summary>
    /// <typeparam name="T">The type to extend.</typeparam>
    extension<T>(T)
        where T : allows ref struct
    {
        /// <summary>
        /// Gets the type handle.
        /// </summary>
        public static RuntimeTypeHandle TypeId
        {
            get
            {
                Ldtoken(Type<T>());
                return Return<RuntimeTypeHandle>();
            }
        }

        /// <summary>
        /// Gets the alignment of the type, in bytes.
        /// </summary>
        public static int Alignment => Unsafe.AlignOf<T>();
    }

    /// <summary>
    /// Provides static extensions for the arbitrary type.
    /// </summary>
    /// <typeparam name="T">The type to extend.</typeparam>
    extension<T>(T)
    {
        /// <summary>
        /// Provides unified behavior of type cast for reference and value types.
        /// </summary>
        /// <remarks>
        /// This method never returns <see langword="null"/> because it treats <see langword="null"/>
        /// value passed to <paramref name="obj"/> as invalid object of type <typeparamref name="T"/>.
        /// </remarks>
        /// <param name="obj">The object to cast.</param>
        /// <returns>The result of conversion.</returns>
        /// <exception cref="InvalidCastException"><paramref name="obj"/> is <see langword="null"/> or not of type <typeparamref name="T"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Cast(object? obj)
        {
            if (obj is null)
                InvalidCastException.Throw();

            return (T)obj;
        }
    }
}