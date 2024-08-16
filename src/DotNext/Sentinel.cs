namespace DotNext;

internal static class Sentinel
{
    // instance has fixed address in memory which is critical to ValueReference implementation
    internal static readonly object Instance = GC.AllocateUninitializedArray<byte>(length: 0, pinned: true);
}