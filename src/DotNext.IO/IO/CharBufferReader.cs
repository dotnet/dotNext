using System;
using System.Buffers;
using System.IO;

namespace DotNext.IO
{
    /// <summary>
    /// Represents <see cref="TextReader"/> wrapper for <see cref="ReadOnlySequence{T}"/> type.
    /// </summary>
    internal sealed class CharBufferReader : TextBufferReader
    {
        private readonly ReadOnlySequence<char> sequence;
        private SequencePosition position;

        /// <summary>
        /// Initializes a new reader for the buffer containing characters.
        /// </summary>
        /// <param name="sequence">The buffer containing characters.</param>
        internal CharBufferReader(ReadOnlySequence<char> sequence)
        {
            this.sequence = sequence;
            position = sequence.Start;
        }

        public override int Peek()
            => sequence.TryGet(ref position, out var block, false) && !block.IsEmpty ? block.Span[0] : InvalidChar;

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
        public override string ReadToEnd()
        {
            var tail = sequence.Slice(position);
            position = sequence.End;
            return tail.ToString();
        }

        /// <inheritdoc />
        public override string? ReadLine()
        {
            var start = position;
            var length = 0L;
            var newLine = Environment.NewLine;
            string? defaultResult = null;

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
                    {
                        defaultResult = string.Empty;
                        goto exit;
                    }
                }
            }

            exit:
            return length == 0L ? defaultResult : sequence.Slice(start, length).ToString();
        }

        /// <inheritdoc />
        public override string ToString() => sequence.ToString();
    }
}