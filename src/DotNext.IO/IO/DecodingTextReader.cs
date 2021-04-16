using System;
using System.Buffers;
using System.Text;

namespace DotNext.IO
{
    using Buffers;

    internal sealed class DecodingTextReader : TextBufferReader
    {
        private readonly Encoding encoding;
        private readonly Decoder decoder;
        private readonly MemoryAllocator<char>? allocator;
        private ReadOnlySequence<byte> sequence;
        private MemoryOwner<char> buffer;
        private int charPos, charLen;

        internal DecodingTextReader(ReadOnlySequence<byte> sequence, Encoding encoding, int bufferSize, MemoryAllocator<char>? allocator)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            decoder = encoding.GetDecoder();
            this.sequence = sequence;
            this.allocator = allocator;
            buffer = allocator.Invoke(bufferSize, false);
        }

        private Span<char> Buffer => buffer.Memory.Span;

        private int ReadBuffer()
        {
            charPos = 0;
            return charLen = ReadBuffer(Buffer);
        }

        private int ReadBuffer(Span<char> output)
        {
            var result = 0;

            for (int maxBytes = encoding.GetMaxByteCount(output.Length), bytesConsumed, charsProduced; !sequence.IsEmpty && !output.IsEmpty; maxBytes -= bytesConsumed, result += charsProduced)
            {
                var input = sequence.FirstSpan;
                decoder.Convert(input, output, maxBytes <= input.Length, out bytesConsumed, out charsProduced, out _);
                sequence = sequence.Slice(bytesConsumed);
                output = output.Slice(charsProduced);
            }

            return result;
        }

        public override int Peek()
            => charPos == charLen && ReadBuffer() == 0 ? InvalidChar : buffer[charPos];

        public override int Read(Span<char> buffer)
        {
            int writtenCount, result = 0;
            if (charPos < charLen)
            {
                Buffer.Slice(charPos, charLen - charPos).CopyTo(buffer, out writtenCount);
                charPos += writtenCount;
                buffer = buffer.Slice(writtenCount);
                result += writtenCount;
            }

            while (!buffer.IsEmpty)
            {
                writtenCount = ReadBuffer(buffer);

                if (writtenCount == 0)
                    break;

                buffer = buffer.Slice(writtenCount);
                result += writtenCount;
            }

            return result;
        }

        public override string? ReadLine()
        {
            if (charPos == charLen && ReadBuffer() == 0)
                return null;

            // this variable is needed to save temporary the length of characters that are candidates for line termination string
            var newLineBufferPosition = 0;
            var newLine = Environment.NewLine.AsSpan();

            var result = new BufferWriterSlim<char>(stackalloc char[MemoryRental<char>.StackallocThreshold], allocator);
            try
            {
                do
                {
                    do
                    {
                        var ch = buffer[charPos];
                        if (ch == newLine[newLineBufferPosition])
                        {
                            // skip character which is a part of line termination string
                            if (newLineBufferPosition == newLine.Length - 1)
                            {
                                charPos += 1;
                                goto exit;
                            }

                            newLineBufferPosition += 1;
                            continue;
                        }

                        if ((uint)newLineBufferPosition > 0U)
                            result.Write(newLine.Slice(0, newLineBufferPosition));

                        result.Add(ch);
                        newLineBufferPosition = 0;
                    }
                    while (++charPos < charLen);
                }
                while (ReadBuffer() > 0);

                // add trailing characters recognized as a part of uncompleted line termination
                if ((uint)newLineBufferPosition > 0U)
                    result.Write(newLine.Slice(0, newLineBufferPosition));

                exit:
                return (uint)result.WrittenCount > 0U ? new string(result.WrittenSpan) : string.Empty;
            }
            finally
            {
                result.Dispose();
            }
        }

        public override string ReadToEnd()
        {
            if (sequence.IsEmpty)
                return string.Empty;
#if NETSTANDARD
            var writer = new BufferWriterSlim<char>(charLen - charPos, allocator);
#else
            var length = sequence.Length.Truncate();

            var writer = length <= MemoryRental<char>.StackallocThreshold ?
                new BufferWriterSlim<char>(stackalloc char[length]) :
                new BufferWriterSlim<char>(length);
#endif

            try
            {
                if (charPos < charLen)
                {
                    writer.Write(Buffer.Slice(charPos, charLen - charPos));
                    charPos = charLen;
                }

                // a little optimization here - don't use internal buffer and write directly to a local buffer
                for (int count; ; writer.Advance(count))
                {
                    var localBuf = writer.GetSpan();
                    count = ReadBuffer(localBuf);
                    if (count == 0)
                        break;
                }

                return new string(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                buffer.Dispose();
                buffer = default;
                sequence = default;
            }

            charLen = charPos = 0;

            base.Dispose(disposing);
        }
    }
}