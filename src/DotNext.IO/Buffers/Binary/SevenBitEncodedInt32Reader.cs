using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal struct SevenBitEncodedInt32Reader : IBufferReader, ISupplier<int>
{
    private SevenBitEncodedInteger<uint> value;
    private bool completed;

    readonly int IBufferReader.RemainingBytes => Unsafe.BitCast<bool, byte>(!completed);

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
        => completed = value.Append(MemoryMarshal.GetReference(source)) is false;

    readonly int ISupplier<int>.Invoke() => (int)value.Value;
}