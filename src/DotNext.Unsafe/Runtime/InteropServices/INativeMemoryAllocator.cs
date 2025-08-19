using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

using Runtime;

internal interface INativeMemoryAllocator<T>
    where T : unmanaged
{
    public static abstract bool IsZeroed { get; }
    
    public static bool IsNaturallyAligned => Intrinsics.AlignOf<T>() <= nuint.Size;
    
    private static unsafe nuint GetByteCount(nuint length)
        => checked(length * (uint)sizeof(T));

    public static unsafe T* Allocate<TAllocator>(nuint length)
        where TAllocator : struct, INativeMemoryAllocator<T>
    {
        nuint elementSize = (uint)sizeof(T);
        void* result;
        if (IsNaturallyAligned)
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
                (uint)Intrinsics.AlignOf<T>());

            if (TAllocator.IsZeroed)
                NativeMemory.Clear(result, byteCount);
        }

        return (T*)result;
    }

    public static unsafe void Free(T* address)
    {
        if (IsNaturallyAligned)
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
        var result = IsNaturallyAligned
            ? NativeMemory.Realloc(address, byteCount)
            : NativeMemory.AlignedRealloc(address, byteCount, (uint)Intrinsics.AlignOf<T>());

        return (T*)result;
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct ZeroedAllocator<T> : INativeMemoryAllocator<T>
    where T : unmanaged
{
    static bool INativeMemoryAllocator<T>.IsZeroed => true;
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct DraftAllocator<T> : INativeMemoryAllocator<T>
    where T : unmanaged
{
    static bool INativeMemoryAllocator<T>.IsZeroed => false;
}