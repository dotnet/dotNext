using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents indirection layer for <see cref="IBufferWriter{T}"/> instance.
/// </summary>
/// <param name="writer">The buffer writer.</param>
/// <typeparam name="T">The type of the elements in the buffer.</typeparam>
[StructLayout(LayoutKind.Auto)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct BufferWriterReference<T>(IBufferWriter<T> writer) : IBufferWriter<T>
{
    /// <inheritdoc/>
    void IBufferWriter<T>.Advance(int count) => writer.Advance(count);

    /// <inheritdoc/>
    Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => writer.GetMemory(sizeHint);

    /// <inheritdoc/>
    Span<T> IBufferWriter<T>.GetSpan(int sizeHint) => writer.GetSpan(sizeHint);
}