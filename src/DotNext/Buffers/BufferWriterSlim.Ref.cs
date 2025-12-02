using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

partial struct BufferWriterSlim<T>
{
    /// <summary>
    /// Represents indirection layer to express a reference to <see cref="BufferWriterSlim{T}"/> in generic context.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly ref struct Ref : IBufferWriter<T>, ITypedReference<BufferWriterSlim<T>>
    {
        private readonly LocalReference<BufferWriterSlim<T>> reference;

        /// <summary>
        /// Initializes a new reference.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        public Ref(ref BufferWriterSlim<T> writer)
            => reference = new(ref writer);

        void IBufferWriter<T>.Advance(int count) => reference.Value.Advance(count);

        Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => reference.Value.GetMemory(sizeHint);

        Span<T> IBufferWriter<T>.GetSpan(int sizeHint) => reference.Value.GetSpan(sizeHint);

        internal ref BufferWriterSlim<T> Value => ref reference.Value;

        ref readonly BufferWriterSlim<T> ITypedReference<BufferWriterSlim<T>>.Value => ref Value;
    }
}