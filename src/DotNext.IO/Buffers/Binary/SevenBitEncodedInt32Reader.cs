using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal struct SevenBitEncodedInt32Reader : IBufferReader, ISupplier<int>
{
    private SevenBitEncodedInteger<uint> decoder;
    private bool incompleted;

    readonly int IBufferReader.RemainingBytes => Unsafe.BitCast<bool, byte>(incompleted);

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
        => incompleted = decoder.Append(MemoryMarshal.GetReference(source));

    readonly int ISupplier<int>.Invoke() => (int)decoder.Value;
}