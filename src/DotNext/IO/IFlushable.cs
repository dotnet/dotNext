using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Represents a destination of data that can be flushed.
    /// </summary>
    public interface IFlushable
    {
        /// <summary>
        /// Flushes this stream by writing any buffered output to the underlying stream.
        /// </summary>
        /// <exception cref="System.IO.IOException">I/O error occurred.</exception>
        void Flush();

        /// <summary>
        /// Flushes this stream asynchronously by writing any buffered output to the underlying stream.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        Task FlushAsync(CancellationToken token = default) => Task.Factory.StartNew(Flush, token, TaskCreationOptions.None, TaskScheduler.Current);
    }
}
