namespace DotNext;

internal static class TypeSlot
{
    private static volatile int typeIndex;
    
    internal static int Count => typeIndex;
    
    internal static int Allocate() => Interlocked.Increment(ref typeIndex) - 1;

    internal static string ToString(int typeIndex, int valueIndex)
    {
        ulong result = (uint)valueIndex | ((ulong)typeIndex << 32);
        return result.ToString("X", provider: null);
    }
}

internal static class TypeSlot<T>
    where T : allows ref struct
{
    public static readonly int Index = TypeSlot.Allocate();
}