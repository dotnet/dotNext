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
    }
}