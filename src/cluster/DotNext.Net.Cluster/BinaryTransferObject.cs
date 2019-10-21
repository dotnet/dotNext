using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext
{
    using Buffers;

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

        async Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            foreach (var segment in Content)
                using (var array = new ArrayRental<byte>(segment.Length))
                {
                    segment.CopyTo(array.Memory);
                    await output.WriteAsync((byte[])array, 0, segment.Length, token).ConfigureAwait(false);
                }
        }

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            foreach (var segment in Content)
            {
                var result = await output.WriteAsync(segment, token);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
            }
        }
    }
}
