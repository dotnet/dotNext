using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Represents non-resizable memory stream 
    /// which is backed by rented array of bytes.
    /// </summary>
    public sealed class RentedMemoryStream : MemoryStream
    {
        private readonly ArrayPool<byte> pool;

        private RentedMemoryStream(byte[] rentedBuffer, ArrayPool<byte> bufferSource)
            : base(rentedBuffer, 0, rentedBuffer.Length, true, true)
        {
            this.pool = bufferSource;
            SetLength(0L);
        }

        /// <summary>
        /// Initializes a new non-resizable memory stream of rented memory from shared array pool. 
        /// </summary>
        /// <param name="capacity">The recommended capacity of the memory stream.</param>
        /// <param name="pool">The array pool used to rent the underlying buffer.</param>
        public RentedMemoryStream(int capacity, ArrayPool<byte> pool)
            : this(pool.Rent(capacity), pool)
        {
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
            if(disposing)
                pool.Return(GetBuffer());
            base.Dispose(disposing);
        }

        /// <summary>
        /// Asynchronously releases the unmanaged resources used by the
        /// </summary>
        /// <returns>A task that represents the asynchronous dispose operation.</returns>
        public override ValueTask DisposeAsync()
        {
            pool.Return(GetBuffer());
            return base.DisposeAsync();
        }
    }
}