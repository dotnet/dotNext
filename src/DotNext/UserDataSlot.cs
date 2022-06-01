using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext;

internal static class UserDataSlot
{
    private static volatile int typeIndex = -1;

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
public readonly struct UserDataSlot<TValue> : IEquatable<UserDataSlot<TValue>>
{
    private static volatile int valueIndexCounter;
    internal static readonly int TypeIndex = UserDataSlot.Allocate();

    private readonly int valueIndex;

    /// <summary>
    /// Allocates a new data slot.
    /// </summary>
    public UserDataSlot() => valueIndex = Interlocked.Increment(ref valueIndexCounter);

    internal int ValueIndex => valueIndex - 1;

    /// <summary>
    /// Allocates a new data slot.
    /// </summary>
    /// <returns>Allocated data slot.</returns>
    [Obsolete("Use public constructor to allocate the slot")]
    public static UserDataSlot<TValue> Allocate() => new();

    /// <summary>
    /// Gets a value indicating that this object was constructed using <see cref="UserDataSlot{TValue}()"/> constructor.
    /// </summary>
    public bool IsAllocated => valueIndex != 0;

    /// <summary>
    /// Checks whether the two data slots are the same.
    /// </summary>
    /// <param name="other">Other data slot to check.</param>
    /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
    public bool Equals(UserDataSlot<TValue> other) => valueIndex == other.valueIndex;

    /// <summary>
    /// Checks whether the two data slots are the same.
    /// </summary>
    /// <param name="other">Other data slot to check.</param>
    /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
    public override bool Equals([NotNullWhen(true)] object? other) => other is UserDataSlot<TValue> slot && Equals(slot);

    /// <summary>
    /// Computes hash code for this data slot.
    /// </summary>
    /// <returns>Hash code.</returns>
    public override int GetHashCode() => valueIndex;

    /// <summary>
    /// Gets textual representation of this data slot
    /// useful for debugging.
    /// </summary>
    /// <returns>Textual representation of this data slot.</returns>
    public override string ToString() => UserDataSlot.ToString(TypeIndex, valueIndex);

    /// <summary>
    /// Checks whether the two data slots are the same.
    /// </summary>
    /// <param name="first">The first data slot to check.</param>
    /// <param name="second">The second data slot to check.</param>
    /// <returns><see langword="true"/> if both data slots identify the same data key.</returns>
    public static bool operator ==(UserDataSlot<TValue> first, UserDataSlot<TValue> second)
        => first.valueIndex == second.valueIndex;

    /// <summary>
    /// Checks whether the two data slots are not the same.
    /// </summary>
    /// <param name="first">The first data slot to check.</param>
    /// <param name="second">The second data slot to check.</param>
    /// <returns><see langword="false"/> if both data slots identify the same data key.</returns>
    public static bool operator !=(UserDataSlot<TValue> first, UserDataSlot<TValue> second)
        => first.valueIndex != second.valueIndex;
}