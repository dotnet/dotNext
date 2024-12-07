using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal struct SevenBitEncodedIntReader() : IBufferReader, ISupplier<int>
{
    private Leb128<uint> decoder;
    private bool incompleted = true;

    readonly int IBufferReader.RemainingBytes => Unsafe.BitCast<bool, byte>(incompleted);

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
        => incompleted = decoder.Append(MemoryMarshal.GetReference(source));

    readonly int ISupplier<int>.Invoke() => (int)decoder.Value;
}