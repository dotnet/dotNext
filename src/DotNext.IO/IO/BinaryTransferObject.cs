using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Represents transfer object for value of blittable type.
    /// </summary>
    /// <typeparam name="T">The type of encapsulated value.</typeparam>
    public class BinaryTransferObject<T> : IDataTransferObject, ISupplier<T>
        where T : unmanaged
    {
        private T content;

        /// <summary>
        /// Gets or sets a value of blittable type encapsulated by this object.
        /// </summary>
        public ref T Content => ref content;

        /// <inheritdoc/>
        T ISupplier<T>.Invoke() => content;

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => true;

        private static unsafe int Length => sizeof(T);

        /// <inheritdoc/>
        long? IDataTransferObject.Length => Length;

        /// <inheritdoc/>
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteAsync(content, token);
    }

    /// <summary>
    /// Represents binary object.
    /// </summary>
    public class BinaryTransferObject : IDataTransferObject, ISupplier<ReadOnlySequence<byte>>
    {
        /// <summary>
        /// Initializes a new binary DTO.
        /// </summary>
        /// <param name="content">The content of the object.</param>
        public BinaryTransferObject(ReadOnlySequence<byte> content) => Content = content;

        /// <summary>
        /// Initializes a new binary object.
        /// </summary>
        /// <param name="content">The content of the object.</param>
        public BinaryTransferObject(ReadOnlyMemory<byte> content)
            : this(new ReadOnlySequence<byte>(content))
        {
        }

        /// <summary>
        /// Gets stream representing content.
        /// </summary>
        public ReadOnlySequence<byte> Content { get; }

        /// <inheritdoc/>
        ReadOnlySequence<byte> ISupplier<ReadOnlySequence<byte>>.Invoke() => Content;

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc/>
        long? IDataTransferObject.Length => Content.Length;

        /// <inheritdoc/>
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => new ValueTask(writer.WriteAsync(Content, token));

        /// <inheritdoc/>
        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            => transformation.TransformAsync(new SequenceBinaryReader(Content), token);
    }
}
