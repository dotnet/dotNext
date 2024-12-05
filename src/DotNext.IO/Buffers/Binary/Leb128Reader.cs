using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal struct Leb128Reader<T>() : IBufferReader, ISupplier<T>
    where T : struct, IBinaryInteger<T>
{
    private Leb128<T> decoder;
    private bool incompleted = true;

    readonly int IBufferReader.RemainingBytes => Unsafe.BitCast<bool, byte>(incompleted);

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
        => incompleted = decoder.Append(MemoryMarshal.GetReference(source));

    readonly T ISupplier<T>.Invoke() => decoder.Value;
}