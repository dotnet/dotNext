using System;
using System.IO;

namespace DotNext.Buffers
{
    internal interface IBufferReader<out T>
    {
        int RemainingBytes { get; }

        void Append(ReadOnlySpan<byte> block, ref int consumedBytes);

        T Complete();

        void EndOfStream() => throw new EndOfStreamException();
    }
}