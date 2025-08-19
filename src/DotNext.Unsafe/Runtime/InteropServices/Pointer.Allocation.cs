using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

partial struct Pointer<T>
{
    internal static unsafe Pointer<T> Allocate()
    {
        var address = INativeMemoryAllocator<T>.IsNaturallyAligned
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
        else if (INativeMemoryAllocator<T>.IsNaturallyAligned)
        {
            NativeMemory.Free(pointer);
        }
        else
        {
            NativeMemory.AlignedFree(pointer);
        }
    }
}