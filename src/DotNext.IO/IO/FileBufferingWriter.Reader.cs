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
            private readonly Stream source;
            private ReadSession session;

            internal ReaderStream(FileBufferingWriter writer, bool useAsyncIO)
            {
                source = writer.GetWrittenContentAsStream(useAsyncIO);
                session = writer.EnterReadMode(this);
            }

            public override long Position
            {
                get => source.Position;
                set => source.Position = value;
            }

            public override long Length => source.Length;

            public override void SetLength(long value)
                => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin)
                => source.Seek(offset, origin);

            public override bool CanRead => true;

            public override bool CanWrite => false;

            public override bool CanSeek => source.CanSeek;

            public override bool CanTimeout => source.CanTimeout;

            public override int ReadByte()
                => source.ReadByte();

            public override int Read(Span<byte> output)
                => source.Read(output);

            public override int Read(byte[] buffer, int offset, int count)
                => source.Read(buffer, offset, count);

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
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
                => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.FromException(new NotSupportedException());

            public override void WriteByte(byte value) => throw new NotSupportedException();

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
                => throw new NotSupportedException();

            public override void EndWrite(IAsyncResult ar) => throw new InvalidOperationException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    source.Dispose();
                    session.Dispose();
                    session = default;
                }

                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                await source.DisposeAsync().ConfigureAwait(false);
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }

        private Stream GetWrittenContentAsStream(bool useAsyncIO)
        {
            Stream result;
            if (fileBackend is null)
            {
                result = StreamSource.AsStream(buffer.Memory.Slice(0, position));
            }
            else
            {
                const FileOptions withAsyncIO = FileOptions.Asynchronous | FileOptions.SequentialScan;
                const FileOptions withoutAsyncIO = FileOptions.SequentialScan;
                result = new FileStream(fileBackend.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, fileProvider.BufferSize, useAsyncIO ? withAsyncIO : withoutAsyncIO);
            }

            return result;
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

            if (fileBackend is not null)
            {
                PersistBuffer();
                fileBackend.Flush(true);
            }

            return new ReaderStream(this, false);
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

            if (fileBackend is not null)
            {
                await PersistBufferAsync(token).ConfigureAwait(false);
                await fileBackend.FlushAsync(token).ConfigureAwait(false);
            }

            return new ReaderStream(this, true);
        }
    }
}