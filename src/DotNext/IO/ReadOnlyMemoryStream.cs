using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using static Buffers.BufferHelpers;

    internal sealed class ReadOnlyMemoryStream : ReadOnlyStream
    {
        // TODO: Move to GetOffset method in .NET 6
        private ReadOnlySequence<byte> sequence;
        private long position;

        internal ReadOnlyMemoryStream(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            position = 0L;
        }

        private ReadOnlySequence<byte> RemainingSequence => sequence.Slice(position);

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
            foreach (var segment in RemainingSequence)
                await destination.WriteAsync(segment, token).ConfigureAwait(false);

            position = sequence.Length;
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            foreach (var segment in RemainingSequence)
                destination.Write(segment.Span);

            position = sequence.Length;
        }

        public override void SetLength(long value)
        {
            sequence = sequence.Slice(0L, value);
            position = Math.Min(position, sequence.Length);
        }

        public override int Read(Span<byte> buffer)
        {
            RemainingSequence.CopyTo(buffer, out var writtenCount);
            position += writtenCount;
            return writtenCount;
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