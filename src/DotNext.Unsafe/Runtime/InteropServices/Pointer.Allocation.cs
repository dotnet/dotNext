using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

partial struct Pointer<T>
{
    private static bool IsNaturallyAligned => Intrinsics.AlignOf<T>() <= nuint.Size;

    internal static unsafe Pointer<T> Allocate()
    {
        var address = IsNaturallyAligned
            ? NativeMemory.Alloc((uint)sizeof(T))
            : NativeMemory.AlignedAlloc((uint)sizeof(T), (uint)Intrinsics.AlignOf<T>());

        return new((T*)address);
    }

    internal static unsafe void Free(Pointer<T> pointer)
    {
        if (pointer.IsNull)
        {
            // nothing to do
        }
        else if (IsNaturallyAligned)
        {
            NativeMemory.Free(pointer);
        }
        else
        {
            NativeMemory.AlignedFree(pointer);
        }
    }
}