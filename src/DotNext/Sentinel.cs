using System.Runtime.InteropServices;

namespace DotNext;

internal static class Sentinel
{
    // instance has a fixed address in memory which is critical to ValueReference implementation
    internal static readonly object Instance = GC.AllocateUninitializedArray<Dummy>(length: 0, pinned: true);

    // The struct is needed to avoid false positives caused by the type check on the variable that stores singleton
    [StructLayout(LayoutKind.Auto)]
    private readonly struct Dummy;
}