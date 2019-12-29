using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Missing = System.Reflection.Missing;

namespace DotNext.IO
{
    using Buffers;
    using DecodingContext = Text.DecodingContext;

    /// <summary>
    /// Represents binary reader for the sequence of bytes.
    /// </summary>
    public struct SequenceBinaryReader : IAsyncBinaryReader
    {
        private readonly ReadOnlySequence<byte> sequence;
        private SequencePosition position;

        internal SequenceBinaryReader(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            position = sequence.Start;
        }

        private TResult Read<TResult, TParser>(TParser parser)
            where TParser : struct, IBufferReader<TResult>
        {
            parser.Append<TResult, TParser>(sequence.Slice(position), out position);
            return parser.RemainingBytes == 0 ? parser.Complete() : throw new EndOfStreamException();
        }

        public T Read<T>() where T : unmanaged => Read<T, ValueReader<T>>(new ValueReader<T>());

        public void Read(Memory<byte> output) => Read<Missing, MemoryReader>(new MemoryReader(output));

        public unsafe string ReadString(int length, in DecodingContext context)
        {
            if(length > 1024)
            {
                var buffer = new ArrayBuffer<char>(length);
                return Read<string, StringReader<ArrayBuffer<char>>>(new StringReader<ArrayBuffer<char>>(in context, buffer));
            }
            else
            {
                var buffer = stackalloc char[length];
                return Read<string, StringReader<UnsafeBuffer<char>>>(new StringReader<UnsafeBuffer<char>>(in context, new UnsafeBuffer<char>(buffer, length)));
            }
        }

        public string ReadString(StringLengthEncoding lengthFormat, in DecodingContext context)
        {
            int length;
            var littleEndian = BitConverter.IsLittleEndian;
            switch(lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.Plain:
                    length = Read<int>();
                    break;
                case StringLengthEncoding.PlainLittleEndian:
                    littleEndian = true;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    littleEndian = false;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
                    length = Read<int, SevenBitEncodedIntReader>(new SevenBitEncodedIntReader());
                    break;
            }
            length.ReverseIfNeeded(littleEndian);
            return ReadString(length, in context);
        }

        ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return new ValueTask<T>(Read<T>());
        }

        ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Read(output);
            return new ValueTask();
        }

        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return new ValueTask<string>(ReadString(length, in context));
        }

        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(StringLengthEncoding lengthEncoding, DecodingContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return new ValueTask<string>(ReadString(lengthEncoding, in context));
        }
    }
}