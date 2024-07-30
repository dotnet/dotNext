using System.Runtime.InteropServices;

namespace DotNext;

internal static class UserDataSlot
{
    private static volatile int typeIndex = -1;

    internal static int SlotTypesCount => typeIndex + 1;

    internal static int Allocate() => Interlocked.Increment(ref typeIndex);

    internal static string ToString(int typeIndex, int valueIndex)
    {
        ulong result = (uint)valueIndex | ((ulong)typeIndex << 32);
        return result.ToString("X", provider: null);
    }
}

/// <summary>
/// Uniquely identifies user data which can be associated
/// with any object.
/// </summary>
/// <typeparam name="TValue">The type of the value stored in user data slot.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct UserDataSlot<TValue>() : IEquatable<UserDataSlot<TValue>>
{
    private static volatile int valueIndexCounter;
    internal static readonly int TypeIndex = UserDataSlot.Allocate();

    private readonly int valueIndex = Interlocked.Increment(ref valueIndexCounter);

    internal int ValueIndex => valueIndex - 1;

    /// <summary>
    /// Gets a value indicating that this object was constructed using <see cref="UserDataSlot{TValue}()"/> constructor.
    /// </summary>
    public bool IsAllocated => valueIndex is not 0;

    /// <summary>
    /// Gets textual representation of this data slot
    /// useful for debugging.
    /// </summary>
    /// <returns>Textual representation of this data slot.</returns>
    public override string ToString() => UserDataSlot.ToString(TypeIndex, valueIndex);
}