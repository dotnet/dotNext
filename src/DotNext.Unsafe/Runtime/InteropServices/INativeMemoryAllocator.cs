using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

using CompilerServices;

internal interface INativeMemoryAllocator<T>
    where T : unmanaged
{
    public static abstract bool IsZeroed { get; }
    
    private static unsafe nuint GetByteCount(nuint length)
        => checked(length * (uint)sizeof(T));

    public static unsafe T* Allocate<TAllocator>(nuint length)
        where TAllocator : struct, INativeMemoryAllocator<T>, allows ref struct
    {
        nuint elementSize = (uint)sizeof(T);
        void* result;
        if (Unsafe.IsNaturallyAligned<T>())
        {
            result = TAllocator.IsZeroed
                ? NativeMemory.AllocZeroed(length, elementSize)
                : NativeMemory.Alloc(length, elementSize);
        }
        else
        {
            var byteCount = GetByteCount(length);
            result = NativeMemory.AlignedAlloc(
                byteCount,
                (uint)Unsafe.AlignOf<T>());

            if (TAllocator.IsZeroed)
                NativeMemory.Clear(result, byteCount);
        }

        return (T*)result;
    }

    public static unsafe void Free(T* address)
    {
        if (Unsafe.IsNaturallyAligned<T>())
        {
            NativeMemory.Free(address);
        }
        else
        {
            NativeMemory.AlignedFree(address);
        }
    }

    public static unsafe T* Realloc(T* address, nuint length)
    {
        var byteCount = GetByteCount(length);
        var result = Unsafe.IsNaturallyAligned<T>()
            ? NativeMemory.Realloc(address, byteCount)
            : NativeMemory.AlignedRealloc(address, byteCount, (uint)Unsafe.AlignOf<T>());

        return (T*)result;
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly ref struct ZeroedAllocator<T> : INativeMemoryAllocator<T>
    where T : unmanaged
{
    static bool INativeMemoryAllocator<T>.IsZeroed => true;
}

[StructLayout(LayoutKind.Auto)]
internal readonly ref struct DraftAllocator<T> : INativeMemoryAllocator<T>
    where T : unmanaged
{
    static bool INativeMemoryAllocator<T>.IsZeroed => false;
}