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
    private const int AllocatedFlag = int.MinValue;
    
    private static volatile int valueIndexCounter;
    internal static int TypeIndex => TypeSlot<TValue>.Index;
    
    private readonly int valueIndex = AllocatedFlag | (Interlocked.Increment(ref valueIndexCounter) - 1);

    internal int ValueIndex => valueIndex & ~AllocatedFlag;

    /// <summary>
    /// Gets a value indicating that this object was constructed by <see cref="UserDataSlot{TValue}()"/> constructor.
    /// </summary>
    public bool IsAllocated => (valueIndex & AllocatedFlag) is not 0;

    /// <summary>
    /// Gets textual representation of this data slot
    /// useful for debugging.
    /// </summary>
    /// <returns>Textual representation of this data slot.</returns>
    public override string ToString() => TypeSlot.ToString(TypeIndex, ValueIndex);
}