using System.Runtime.InteropServices;

namespace DotNext;

/// <summary>
/// Uniquely identifies user data which can be associated
/// with any object.
/// </summary>
/// <typeparam name="TValue">The type of the value stored in user data slot.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct UserDataSlot<TValue>()
{
    private static volatile int valueIndexCounter;
    internal static int TypeIndex => TypeSlot<TValue>.Index;

    internal int ValueIndex { get; } = Interlocked.Increment(ref valueIndexCounter) - 1;

    /// <summary>
    /// Gets a value indicating that this object was constructed using <see cref="UserDataSlot{TValue}()"/> constructor.
    /// </summary>
    public bool IsAllocated => ValueIndex is not 0;

    /// <summary>
    /// Gets textual representation of this data slot
    /// useful for debugging.
    /// </summary>
    /// <returns>Textual representation of this data slot.</returns>
    public override string ToString() => TypeSlot.ToString(TypeIndex, ValueIndex);
}