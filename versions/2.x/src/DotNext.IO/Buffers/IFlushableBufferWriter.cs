using System.Buffers;

namespace DotNext.Buffers
{
    using IFlushable = IO.IFlushable;

    /// <summary>
    /// Represents buffer writer that supports flushing.
    /// </summary>
    /// <typeparam name="T">The type of the items to be written.</typeparam>
    public interface IFlushableBufferWriter<T> : IBufferWriter<T>, IFlushable
    {
    }
}