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
    public class BinaryTransferObject<T> : IDataTransferObject
        where T : unmanaged
    {
        private T content;

        /// <summary>
        /// Gets a value of blittable type encapsulated by this object.
        /// </summary>
        public ref T Content => ref content;

        bool IDataTransferObject.IsReusable => true;

        private unsafe static int Length => sizeof(T);

        long? IDataTransferObject.Length => Length;
    
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteAsync(content, token);
    }

    /// <summary>
    /// Represents binary object.
    /// </summary>
    public class BinaryTransferObject : IDataTransferObject
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

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => Content.Length;

        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            foreach (var segment in Content)
                await writer.WriteAsync(segment, token).ConfigureAwait(false);
        }
    }
}
