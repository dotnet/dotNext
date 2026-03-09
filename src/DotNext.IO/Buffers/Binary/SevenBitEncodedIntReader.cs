using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

using Runtime;
using Runtime.CompilerServices;

[StructLayout(LayoutKind.Auto)]
internal struct SevenBitEncodedIntReader() : IBufferReader, ISupplier<int>
{
    private Leb128<uint> decoder;
    private bool incompleted = true;

    readonly int IBufferReader.RemainingBytes => Unsafe.BitCast<bool, byte>(incompleted);

    void IConsumer<ReadOnlySpan<byte>>.Invoke(ReadOnlySpan<byte> source)
        => incompleted = decoder.Append(MemoryMarshal.GetReference(source));

    readonly int ISupplier<int>.Invoke() => (int)decoder.Value;

    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
        => throw new NotSupportedException();
}