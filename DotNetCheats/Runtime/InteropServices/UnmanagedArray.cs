using System;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.Marshal;

namespace Cheats.Runtime.InteropServices
{
    // public unsafe readonly struct UnmanagedArray<T>: IUnmanagedMemory<T>
    //     where T: unmanaged
    // {
    //     public static readonly int ElementSize = SizeOf<T>();

    //     private readonly T* pointer;

    //     public UnmanagedArray(int length)
    //     {
    //         if(length < 0)
    //             throw new ArgumentOutOfRangeException("Length of the array should not be less than zero");
    //         Length = length;
    //         var size = length * ElementSize;
    //         pointer = (T*)AllocHGlobal(size);
    //         Unsafe.InitBlock(pointer, 0, (uint)size);
    //     }

    //     /// <summary>
    //     /// Gets length of this array.
    //     /// </summary>
    //     public int Length { get; }

    //     /// <summary>
    //     /// Size of allocated  memory.
    //     /// </summary>
    //     public int Size => ElementSize * Length;

    //     ulong IUnmanagedMemory<T>.Size => (uint)Size;

    //     T* IUnmanagedMemory<T>.Address => pointer;

    //     public void ZeroMem()
    //     {
    //         if(pointer != Memory.NullPtr)
    //             Unsafe.InitBlock(pointer, 0, (uint)Size);
    //     }

    //     private T* Offset(int index)
    //     {
    //         if(pointer == Memory.NullPtr)
    //             throw new NullPointerException();
    //         else if(index >= 0 && index < Length) 
    //             return pointer + index;
    //         else 
    //             throw new IndexOutOfRangeException($"Index should be in range [0, {Length})");
    //     }

    //     public T this[int index]
    //     {
    //         get => *Offset(index);
    //         set => *Offset(index) = value;
    //     }
        

    //     [CLSCompliant(false)]
    //     public T* At(int index) => Offset(index);

    //     [CLSCompliant(false)]
    //     public static implicit operator T*(UnmanagedArray<T> array) => array.pointer;

    //     public static implicit operator ReadOnlySpan<T>(in UnmanagedArray<T> array)
    //         => array.pointer == Memory.NullPtr ? new ReadOnlySpan<T>() : new ReadOnlySpan<T>(array.pointer, array.Length);

    //     /// <summary>
    //     /// Releases unmanaged memory associated with the array.
    //     /// </summary>
    //     public void Dispose()
    //     {
    //         var pointer = new IntPtr(this.pointer);
    //         if(pointer != IntPtr.Zero)
    //             FreeHGlobal(pointer);
    //     }
    // }
}