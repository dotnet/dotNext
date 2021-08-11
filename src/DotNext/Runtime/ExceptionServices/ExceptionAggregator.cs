using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.ExceptionServices
{
    /// <summary>
    /// Represents aggregator of multiple exceptions.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct ExceptionAggregator : ISupplier<Exception?>
    {
        private object? exceptionInfo;

        /// <summary>
        /// Gets a value indicating that this object has no inner exceptions.
        /// </summary>
        public readonly bool IsEmpty => exceptionInfo is null;

        /// <summary>
        /// Aggregates exception.
        /// </summary>
        /// <param name="e">The exception to add.</param>
        public void Add(Exception e)
        {
            switch (exceptionInfo)
            {
                case null:
                    exceptionInfo = ExceptionDispatchInfo.Capture(e);
                    break;
                case ExceptionDispatchInfo info:
                    exceptionInfo = CreateList(info.SourceException, e);
                    break;
                case ICollection<Exception> exceptions:
                    exceptions.Add(e);
                    break;
            }

            static ICollection<Exception> CreateList(Exception first, Exception second)
            {
                ICollection<Exception> result = new LinkedList<Exception>();
                result.Add(first);
                result.Add(second);
                return result;
            }
        }

        /// <summary>
        /// Creates aggregated exception.
        /// </summary>
        /// <returns>The aggregated exception; or <see langword="null"/> if this object has no aggregated exceptions.</returns>
        public readonly Exception? CreateException() => exceptionInfo switch
        {
            ExceptionDispatchInfo info => info.SourceException,
            IEnumerable<Exception> exceptions => new AggregateException(exceptions),
            _ => null,
        };

        /// <inheritdoc/>
        readonly Exception? ISupplier<Exception?>.Invoke() => CreateException();

        /// <summary>
        /// Throws aggregated exception if this object contains at least one exception.
        /// </summary>
        /// <exception cref="AggregateException">This object contains two or more aggregated exceptions.</exception>
        /// <exception cref="Exception">This object contains single exception.</exception>
        public readonly void ThrowIfNeeded()
        {
            switch (exceptionInfo)
            {
                case ExceptionDispatchInfo info:
                    info.Throw();
                    break;
                case ICollection<Exception> exceptions:
                    var e = new AggregateException(exceptions);
                    exceptions.Clear(); // help GC
                    throw e;
            }
        }
    }
}