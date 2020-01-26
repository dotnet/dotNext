using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Represents a portion of the underlying stream in the its of the read-onyl view.
    /// </summary>
    /// <remarks>
    /// The segmentation is supported only for seekable streams.
    /// </remarks>
    public sealed class StreamSegment : Stream
    {
        private readonly bool leaveOpen;
        private long length, position;

        /// <summary>
        /// Initializes a new segment of the specified stream.
        /// </summary>
        /// <param name="stream">The underlying stream represented by the segment.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open after the object is disposed; otherwise, <see langword="false"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        public StreamSegment(Stream stream, bool leaveOpen = true)
        {
            BaseStream = stream ?? throw new ArgumentNullException(nameof(stream));
            length = stream.Length;
            position = 0L;
            this.leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Gets underlying stream.
        /// </summary>
        public Stream BaseStream { get; }

        /// <summary>
        /// Establishes segment bounds.
        /// </summary>
        /// <remarks>
        /// This method modifies <see cref="Stream.Position"/> property of the underlying stream.
        /// </remarks>
        /// <param name="offset">The offset in the underlying stream.</param>
        /// <param name="length">The length of the segment.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is larger than the reamining length of the underlying stream; or <paramref name="offset"/> if greater than the length of the underlying stream.</exception>
        public void Adjust(long offset, long length)
        {
            if (offset < 0L || offset > BaseStream.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0L || length > BaseStream.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(length));
            this.length = length;
            Position = 0L;
            BaseStream.Position = offset;
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <value><see langword="true"/> if the stream supports reading; otherwise, <see langword="false"/>.</value>
        public override bool CanRead => BaseStream.CanRead;

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <value><see langword="true"/> if the stream supports seeking; otherwise, <see langword="false"/>.</value>
        public override bool CanSeek => BaseStream.CanSeek;

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <value>Always <see langword="false"/>.</value>
        public override bool CanWrite => false;

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        public override long Length => length;

        /// <summary>
        /// Gets or sets relative position from the beginning of this segment.
        /// </summary>
        public override long Position
        {
            get => position;
            set
            {
                if (value < 0L || value > length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                position = value;
            }
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush() => BaseStream.Flush();

        /// <summary>
        /// Asynchronously clears all buffers for this stream, causes any buffered data to  be written to the underlying device, and monitors cancellation requests.
        /// </summary>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous flush operation.</returns>
        public override Task FlushAsync(CancellationToken token = default) => BaseStream.FlushAsync(token);

        /// <summary>
        /// Gets a value that determines whether the current stream can time out.
        /// </summary>
        /// <value>A value that determines whether the current stream can time out.</value>
        public override bool CanTimeout => BaseStream.CanTimeout;

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
        /// </summary>
        /// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</returns>
        public override int ReadByte()
        {
            if (position >= length)
                return -1;
            position += 1;
            return BaseStream.ReadByte();
        }

        /// <summary>
        /// This method is not supported.
        /// </summary>
        /// <param name="value">The byte to write to the stream.</param>
        /// <exception cref="NotSupportedException">The method is not supported.</exception>
        public override void WriteByte(byte value) => throw new NotSupportedException();

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">Contains the specified byte array with the values between <paramref name="offset"/> and <c>(offset + count - 1)</c> replaced by the bytes read from the current source.</param>
        /// <param name="offset"> The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = BaseStream.Read(buffer, offset, Math.Min(count, (int)(length - position)));
            position += count;
            return count;
        }

        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">Contains the specified byte array with the values between <paramref name="offset"/> and <c>(offset + count - 1)</c> replaced by the bytes read from the current source.</param>
        /// <param name="offset"> The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        {
            count = await BaseStream.ReadAsync(buffer, offset, Math.Min(count, (int)(length - position))).ConfigureAwait(false);
            position += count;
            return count;
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the origin parameter.</param>
        /// <param name="origin">The reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition += offset;
                    break;
                case SeekOrigin.End:
                    newPosition = length + offset;
                    break;
            }
            if (newPosition > length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            position = newPosition;
            return newPosition;
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is greater than remaining length of the stream.</exception>
        public override void SetLength(long value)
        {
            if (value > BaseStream.Length - BaseStream.Position)
                throw new ArgumentOutOfRangeException(nameof(value));
            length = value;
        }

        /// <summary>
        /// This method is not supported.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <exception cref="NotSupportedException">The method is not supported.</exception>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <summary>
        /// This method is not supported.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">The method is not supported.</exception>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token = default) => Task.FromException(new NotSupportedException());

        /// <summary>
        /// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
        /// </summary>
        /// <value> A value, in miliseconds, that determines how long the stream will attempt to read before timing out.</value>
        public override int ReadTimeout
        {
            get => BaseStream.ReadTimeout;
            set => BaseStream.ReadTimeout = value;
        }

        /// <summary>
        /// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out.
        /// </summary>
        /// <value>A value, in miliseconds, that determines how long the stream will attempt to write before timing out.</value>
        public override int WriteTimeout
        {
            get => BaseStream.WriteTimeout;
            set => BaseStream.WriteTimeout = value;
        }

        /// <summary>
        ///  Releases the unmanaged resources used by this stream and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
                BaseStream.Dispose();
            base.Dispose(disposing);
        }
    }
}
