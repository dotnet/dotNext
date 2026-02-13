using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

using CompilerServices;

partial struct Pointer<T>
{
    internal static unsafe Pointer<T> Allocate()
    {
        var address = Unsafe.CanBeNativelyAligned<T>()
            ? NativeMemory.Alloc((uint)sizeof(T))
            : NativeMemory.AlignedAlloc((uint)sizeof(T), (uint)Unsafe.AlignOf<T>());

        return new((T*)address);
    }

    internal static unsafe void Free(Pointer<T> pointer)
    {
        if (pointer.IsNull)
        {
            // nothing to do
        }
        else if (Unsafe.CanBeNativelyAligned<T>())
        {
            NativeMemory.Free(pointer);
        }
        else
        {
            NativeMemory.AlignedFree(pointer);
        }
    }
}