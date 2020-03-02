namespace DotNext.IO
{
    /// <summary>
    /// Represents a destination of data that can be flushed
    /// </summary>
    public interface IFlushable
    {
        /// <summary>
        /// Flushes this stream by writing any buffered output to the underlying stream.
        /// </summary>
        /// <exception cref="System.IO.IOException">I/O error occurred.</exception>
        void Flush();
    }
}
