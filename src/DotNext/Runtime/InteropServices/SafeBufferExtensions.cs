using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents extensions for <see cref="SafeBuffer"/> type.
    /// </summary>
    public static class SafeBufferExtensions
    {
        /// <summary>
        /// Obtains managed pointer from buffer for a block of memory.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>A managed pointer to the buffer; or <see cref="Unsafe.NullRef{T}"/> if buffer is invalid.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static unsafe ref byte AcquirePointer(this SafeBuffer buffer)
        {
            byte* ptr = null;
            buffer.AcquirePointer(ref ptr);
            return ref ptr[0];
        }
    }
}