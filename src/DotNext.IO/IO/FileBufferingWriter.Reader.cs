using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    public partial class FileBufferingWriter
    {
        private sealed class ReaderStream : Stream
        {
            private const long DoNotRestorePosition = long.MinValue;
            private readonly Stream source;
            private readonly long initialPosition;
            private ReadSession session;

            internal ReaderStream(FileBufferingWriter writer)
            {
                source = writer.GetWrittenContentAsStream(out bool persisted);
                if (persisted)
                {
                    initialPosition = source.Position;
                    source.Position = 0L;
                }
                else
                {
                    initialPosition = DoNotRestorePosition;
                }

                session = writer.EnableReadMode(this);
            }

            public override long Position
            {
                get => source.Position;
                set => throw new NotSupportedException();
            }

            public override long Length => source.Length;

            public override void SetLength(long value)
                => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin)
                => throw new NotSupportedException();

            public override bool CanRead => true;

            public override bool CanWrite => false;

            public override bool CanSeek => false;

            public override bool CanTimeout => source.CanTimeout;

            public override int ReadByte()
                => source.ReadByte();

            public override int Read(Span<byte> output)
                => source.Read(output);

            public override int Read(byte[] buffer, int offset, int count)
                => source.Read(buffer, offset, count);

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object? state)
                => source.BeginRead(buffer, offset, count, callback, state);

            public override int EndRead(IAsyncResult asyncResult)
                => source.EndRead(asyncResult);

            public override ValueTask<int> ReadAsync(Memory<byte> output, CancellationToken token)
                => source.ReadAsync(output, token);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
                => source.ReadAsync(buffer, offset, count, token);

            public override void Flush() => source.Flush();

            public override Task FlushAsync(CancellationToken token) => source.FlushAsync(token);

            public override void CopyTo(Stream destination, int bufferSize)
                => source.CopyTo(destination, bufferSize);

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
                => source.CopyToAsync(destination, bufferSize, token);

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
                => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException());

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
                => new ValueTask(token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException()));

            public override void WriteByte(byte value) => throw new NotSupportedException();

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
                => throw new NotSupportedException();

            public override void EndWrite(IAsyncResult ar) => throw new InvalidOperationException();

            private void CleanupUnderlyingStream()
            {
                if (initialPosition == DoNotRestorePosition)
                        source.Dispose();
                else
                    source.Position = initialPosition;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    CleanupUnderlyingStream();
                    session.Dispose();
                    session = default;
                }

                base.Dispose(disposing);
            }

            public override ValueTask DisposeAsync()
            {
                CleanupUnderlyingStream();
                return base.DisposeAsync();
            }
        }

        private Stream GetWrittenContentAsStream(out bool persisted)
        {
            if (fileBackend is null)
            {
                persisted = false;
                return StreamSource.AsStream(buffer.Memory.Slice(0, position));
            }

            persisted = true;
            return fileBackend;
        }

        /// <summary>
        /// Gets written content as read-only stream.
        /// </summary>
        /// <returns>Read-only stream representing the written content.</returns>
        /// <exception cref="InvalidOperationException">The stream is already obtained but not disposed.</exception>
        public Stream GetWrittenContentAsStream()
        {
            if (IsReading)
                throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

            if (!(fileBackend is null))
            {
                PersistBuffer();
                fileBackend.Flush(true);
            }

            return new ReaderStream(this);
        }

        /// <summary>
        /// Gets written content as read-only stream asynchronously.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Read-only stream representing the written content.</returns>
        /// <exception cref="InvalidOperationException">The stream is already obtained but not disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async ValueTask<Stream> GetWrittenContentAsStreamAsync(CancellationToken token = default)
        {
            if (IsReading)
                throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

            if (!(fileBackend is null))
            {
                await PersistBufferAsync(token).ConfigureAwait(false);
                await fileBackend.FlushAsync(token).ConfigureAwait(false);
            }

            return new ReaderStream(this);
        }
    }
}