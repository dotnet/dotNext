using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

partial struct Pointer<T>
{
    /// <summary>
    /// Allocates a new unmanaged memory for type <typeparamref name="T"/> with proper alignment.
    /// </summary>
    /// <returns>A handle representing the allocated memory.</returns>
    public static ValueHandle Allocate() => new();

    /// <summary>
    /// Allocates a new unmanaged memory for type <typeparamref name="T"/> with proper alignment.
    /// </summary>
    /// <param name="value">The value to be placed to the allocated space.</param>
    /// <returns>A handle representing the allocated memory.</returns>
    public static ValueHandle Allocate(T value)
    {
        var handle = Allocate();
        handle.Pointer.Value = value;
        return handle;
    }

    /// <summary>
    /// Represents allocation scope.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ValueHandle : IDisposable
    {
        /// <summary>
        /// Represents a pointer to the allocated memory.
        /// </summary>
        public readonly Pointer<T> Pointer;

        /// <summary>
        /// Allocates a new unmanaged memory with proper alignment.
        /// </summary>
        public ValueHandle()
        {
            var memPtr = NativeMemory.AlignedAlloc((uint)sizeof(T), (uint)Intrinsics.AlignOf<T>());
            Pointer = new((T*)memPtr);
        }

        /// <summary>
        /// Releases the unmanaged memory.
        /// </summary>
        void IDisposable.Dispose() => NativeMemory.AlignedFree(Pointer);
    }
}