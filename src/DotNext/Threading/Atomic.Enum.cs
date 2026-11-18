using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext.Threading;

using Diagnostics.CodeAnalysis;
using Runtime.CompilerServices;

partial class Atomic
{
    /// <summary>
    /// Writes atomically the enum value at the specified location in the memory.
    /// </summary>
    /// <remarks>
    /// This method works correctly on 32-bit and 64-bit architectures,
    /// including enums with 64-bit underlying type.
    /// </remarks>
    /// <typeparam name="TEnum">The type of the enum.</typeparam>
    /// <param name="location">The location of the value.</param>
    /// <param name="value">The desired value at the specified location.</param>
    [MemoryOrdering(MemoryOrdering.Release)]
    public static void Write<TEnum>(ref TEnum location, TEnum value)
        where TEnum : struct, Enum
    {
        if (IsAtomic<TEnum>())
        {
            VolatileWrite(ref location, value);
        }
        else
        {
            Interlocked.Exchange(ref Unsafe.As<TEnum, long>(ref location), Unsafe.BitCast<TEnum, long>(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void VolatileWrite(ref TEnum location, TEnum value)
        {
            Push(ref location);
            Push(value);
            Volatile();
            Stobj<TEnum>();
        }
    }

    /// <summary>
    /// Reads atomically the enum value from the specified location in the memory.
    /// </summary>
    /// <remarks>
    /// This method works correctly on 32-bit and 64-bit architectures,
    /// including enums with 64-bit underlying type.
    /// </remarks>
    /// <typeparam name="TEnum">The type of the enum.</typeparam>
    /// <param name="location">The location of the value.</param>
    /// <returns>The value at the specified location.</returns>
    [MemoryOrdering(MemoryOrdering.Acquire)]
    public static TEnum Read<TEnum>(ref readonly TEnum location)
        where TEnum : struct, Enum
    {
        return IsAtomic<TEnum>()
            ? VolatileRead(in location)
            : Unsafe.BitCast<long, TEnum>(Interlocked.Read(in Unsafe.InToRef<TEnum, long>(in location)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static TEnum VolatileRead(ref readonly TEnum location)
        {
            PushInRef(in location);
            Volatile();
            Ldobj<TEnum>();
            return Return<TEnum>();
        }
    }
}