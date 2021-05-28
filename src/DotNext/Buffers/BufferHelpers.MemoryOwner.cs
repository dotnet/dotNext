using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers
{
    public static partial class BufferHelpers
    {
        /// <summary>
        /// Gets managed pointer to the first element in the rented memory block.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the memory block.</typeparam>
        /// <param name="owner">The rented memory block.</param>
        /// <returns>A managed pointer to the first element; or <see cref="System.Runtime.CompilerServices.Unsafe.NullRef{T}"/> if memory block is empty.</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static ref T GetReference<T>(in MemoryOwner<T> owner)
            => ref owner.IsEmpty ? ref Unsafe.NullRef<T>() : ref owner.First;

        /// <summary>
        /// Resizes the buffer.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
        /// <param name="owner">The buffer owner to resize.</param>
        /// <param name="newLength">A new length of the buffer.</param>
        /// <param name="exactSize">
        /// <see langword="true"/> to ask allocator to allocate exactly <paramref name="newLength"/>;
        /// <see langword="false"/> to allocate at least <paramref name="newLength"/>.
        /// </param>
        /// <param name="allocator">The allocator to be called if the requested length is larger than the requested length.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="newLength"/> is less than zero.</exception>
        public static void Resize<T>(this ref MemoryOwner<T> owner, int newLength, bool exactSize = true, MemoryAllocator<T>? allocator = null)
        {
            if (!owner.TryResize(newLength))
            {
                var newBuffer = allocator.Invoke(newLength, exactSize);
                owner.Memory.CopyTo(newBuffer.Memory);
                owner.Dispose();
                owner = newBuffer;
            }
        }
    }
}