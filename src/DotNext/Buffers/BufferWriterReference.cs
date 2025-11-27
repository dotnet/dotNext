using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

[StructLayout(LayoutKind.Auto)]
internal readonly struct BufferWriterReference<T>(IBufferWriter<T> writer) : IBufferWriter<T>
{
    void IBufferWriter<T>.Advance(int count) => writer.Advance(count);

    Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => writer.GetMemory(sizeHint);

    Span<T> IBufferWriter<T>.GetSpan(int sizeHint) => writer.GetSpan(sizeHint);
}