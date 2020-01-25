using System.Buffers;
using System.IO;

namespace DotNext.IO
{
    /// <summary>
    /// Represents non-resizable memory stream 
    /// which is backed by rented array of bytes.
    /// </summary>
    public sealed class RentedMemoryStream : MemoryStream
    {
        private readonly ArrayPool<byte> pool;

        /// <summary>
        /// Initializes a new non-resizable memory stream of rented memory from shared array pool. 
        /// </summary>
        /// <param name="capacity">The recommended capacity of the memory stream.</param>
        /// <param name="pool">The array pool used to rent the underlying buffer.</param>
        public RentedMemoryStream(int capacity, ArrayPool<byte> pool)
            : base(pool.Rent(capacity), 0, capacity, true, true)
        {
            this.pool = pool;
            SetLength(0L);
        }

        /// <summary>
        /// Initializes a new non-resizable memory stream of rented memory from shared array pool. 
        /// </summary>
        /// <param name="capacity">The recommended capacity of the memory stream.</param>
        public RentedMemoryStream(int capacity)
            : this(capacity, ArrayPool<byte>.Shared)
        {
        }

        /// <summary>
        /// Releases all resources used by this stream. 
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                pool.Return(GetBuffer());
            base.Dispose(disposing);
        }
    }
}