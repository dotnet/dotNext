using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using static Buffers.BufferHelpers;

    /// <summary>
    /// Represents <see cref="TextReader"/> wrapper for <see cref="ReadOnlySequence{T}"/> type.
    /// </summary>
    public sealed class TextBufferReader : TextReader
    {
        private const int InvalidChar = -1;
        private readonly ReadOnlySequence<char> sequence;
        private SequencePosition position;

        /// <summary>
        /// Initializes a new reader for the buffer containing characters.
        /// </summary>
        /// <param name="sequence">The buffer containing characters.</param>
        public TextBufferReader(ReadOnlySequence<char> sequence)
        {
            this.sequence = sequence;
            position = sequence.Start;
        }

        /// <summary>
        /// Initializes a new reader for the buffer containing characters.
        /// </summary>
        /// <param name="chars">The buffer containing characters.</param>
        public TextBufferReader(ReadOnlyMemory<char> chars)
            : this(new ReadOnlySequence<char>(chars))
        {
        }

        /// <summary>
        /// Resets the reader so it can be used again.
        /// </summary>
        public void Reset() => position = sequence.Start;

        /// <inheritdoc />
        public override int Peek()
            => sequence.TryGet(ref position, out var block, false) && !block.IsEmpty ? block.Span[0] : InvalidChar;

        /// <inheritdoc />
        public override int Read()
        {
            var result = default(char);
            return Read(MemoryMarshal.CreateSpan(ref result, 1)) > 0 ? result : InvalidChar;
        }

        /// <inheritdoc />
        public override int Read(Span<char> buffer)
        {
            int result;
            if (!buffer.IsEmpty && sequence.TryGet(ref position, out var block, false) && !block.IsEmpty)
            {
                block.Span.CopyTo(buffer, out result);
                position = sequence.GetPosition(result, position);
            }
            else
            {
                result = 0;
            }

            return result;
        }

        /// <inheritdoc />
        public override int Read(char[] buffer, int index, int count)
            => Read(buffer.AsSpan(index, count));

        /// <inheritdoc />
        public override int ReadBlock(Span<char> buffer)
        {
            int count, total = 0;
            do
            {
                count = Read(buffer.Slice(total));
                total += count;
            }
            while (count > 0);

            return total;
        }

        /// <inheritdoc />
        public override int ReadBlock(char[] buffer, int index, int count)
            => ReadBlock(buffer.AsSpan(index, count));

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken token = default)
        {
            Task<int> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<int>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<int>(Read(buffer.Span));
                }
                catch (Exception e)
                {
                    result = Task.FromException<int>(e);
                }
            }

            return new ValueTask<int>(result);
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(char[] buffer, int index, int count)
            => ReadAsync(buffer.AsMemory(index, count)).AsTask();

        /// <inheritdoc />
        public override ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken token = default)
        {
            Task<int> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<int>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<int>(ReadBlock(buffer.Span));
                }
                catch (Exception e)
                {
                    result = Task.FromException<int>(e);
                }
            }

            return new ValueTask<int>(result);
        }

        /// <inheritdoc />
        public override Task<int> ReadBlockAsync(char[] buffer, int index, int count)
            => ReadBlockAsync(buffer.AsMemory(index, count)).AsTask();

        /// <inheritdoc />
        public override string ReadToEnd()
        {
            var tail = sequence.Slice(position);
            position = sequence.End;
            return tail.BuildString();
        }

        /// <inheritdoc />
        public override Task<string> ReadToEndAsync()
        {
            Task<string> result;
            try
            {
                result = Task.FromResult<string>(ReadToEnd());
            }
            catch (Exception e)
            {
                result = Task.FromException<string>(e);
            }

            return result;
        }

        /// <inheritdoc />
        public override string? ReadLine()
        {
            var start = position;
            var length = 0L;
            var newLine = Environment.NewLine;

            // this variable is needed to save temporary the length of characters that are candidates for line termination string
            var newLineBufferPosition = 0;
            while (sequence.TryGet(ref position, out var block, false) && !block.IsEmpty)
            {
                foreach (var ch in block.Span)
                {
                    if (ch == newLine[newLineBufferPosition])
                    {
                        // skip character which is a part of line termination string
                        newLineBufferPosition += 1;
                    }
                    else
                    {
                        length += 1L + newLineBufferPosition;
                        newLineBufferPosition = 0;
                    }

                    position = sequence.GetPosition(1L, position);
                    if (newLineBufferPosition >= newLine.Length)
                        goto exit;
                }
            }

            exit:
            return length == 0L ? null : sequence.Slice(start, length).BuildString();
        }

        /// <inheritdoc />
        public override Task<string?> ReadLineAsync()
        {
            Task<string?> result;
            try
            {
                result = Task.FromResult<string?>(ReadLine());
            }
            catch (Exception e)
            {
                result = Task.FromException<string?>(e);
            }

            return result;
        }

        /// <inheritdoc />
        public override string ToString() => sequence.ToString();
    }
}