using System.Runtime.InteropServices;

namespace DotNext.Buffers;

[StructLayout(LayoutKind.Auto)]
internal struct SevenBitEncodedIntReader : IBufferReader<int>
{
    private int remainingBytes;
    private SevenBitEncodedInt.Reader reader;

    internal SevenBitEncodedIntReader(int remainingBytes)
    {
        this.remainingBytes = remainingBytes;
        reader = new SevenBitEncodedInt.Reader();
    }

    readonly int IBufferReader<int>.RemainingBytes => remainingBytes;

    void IBufferReader<int>.Append(scoped ReadOnlySpan<byte> block, scoped ref int consumedBytes)
    {
        consumedBytes = 0;
        foreach (var b in block)
        {
            consumedBytes += 1;
            if (reader.Append(b))
            {
                remainingBytes -= 1;
            }
            else
            {
                remainingBytes = 0;
                break;
            }
        }
    }

    readonly int IBufferReader<int>.Complete() => (int)reader.Result;
}