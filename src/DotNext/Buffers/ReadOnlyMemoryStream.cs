using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Buffers
{
    internal sealed class ReadOnlyMemoryStream : Stream
    {
        private ReadOnlySequence<byte> sequence;
        private long position;

        internal ReadOnlyMemoryStream(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            position = 0L;
        }

        public override bool CanWrite => false;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override long Length => sequence.Length;

        public override long Position
        {
            get => position;
            set
            {
                if (value < 0L || value > sequence.Length)
                    throw new ArgumentOutOfRangeException(nameof(value));

                position = value;
            }
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
        {
            for (var position = sequence.GetPosition(this.position); sequence.TryGet(ref position, out var segment) && !segment.IsEmpty; this.position += segment.Length)
            {
                await destination.WriteAsync(segment, token).ConfigureAwait(false);
            }
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            for (var position = sequence.GetPosition(this.position); sequence.TryGet(ref position, out var segment) && !segment.IsEmpty; this.position += segment.Length)
            {
                destination.Write(segment.Span);
            }
        }

        public override void Flush() { }

        public override Task FlushAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        public override void SetLength(long value)
        {
            sequence = sequence.Slice(0L, value);
            position = Math.Min(position, sequence.Length);
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var remaining = sequence.Length - position;

            if (remaining <= 0L || buffer.Length == 0)
                return 0;
            
            var count = (int)Math.Min(buffer.Length, remaining);
            sequence.Slice(position, count).CopyTo(buffer);
            position += count;
            return count;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new ValueTask<int>(Task.FromCanceled<int>(token));

            return new ValueTask<int>(Read(buffer.Span));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException());

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
            => new ValueTask(token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException()));

        public override void WriteByte(byte value) => throw new NotSupportedException();

        public override int ReadByte()
        {
            byte result = 0;
            return Read(CreateSpan(ref result, 1)) == 1 ? result : -1;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => position + offset,
                SeekOrigin.End => sequence.Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };

            if (newPosition < 0L)
                throw new IOException();

            if (newPosition > sequence.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return position = newPosition;
        }

        public override string ToString() => sequence.ToString();
    }
}