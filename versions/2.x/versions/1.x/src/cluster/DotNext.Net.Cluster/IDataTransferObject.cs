using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext
{
    /// <summary>
    /// Represents data unit that can be transferred over wire.
    /// </summary>
    public interface IDataTransferObject
    {
        /// <summary>
        /// Indicates that the content of this object can be copied to the output stream or pipe multiple times.
        /// </summary>
        bool IsReusable { get; }

        /// <summary>
        /// Gets length of the object payload, in bytes.
        /// </summary>
        /// <remarks>
        /// If value is <see langword="null"/> then length of the payload cannot be determined.
        /// </remarks>
        long? Length { get; }

        /// <summary>
        /// Copies the object content into the specified stream.
        /// </summary>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <param name="output">The output stream receiving object content.</param>
        Task CopyToAsync(Stream output, CancellationToken token = default);

        /// <summary>
        /// Copies the object content into the specified pipe writer.
        /// </summary>
        /// <param name="output">The writer.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        ValueTask CopyToAsync(PipeWriter output, CancellationToken token = default);
    }
}
