using System;

namespace DotNext.IO.Log
{
    /// <summary>
    /// Represents log entry in the audit trail.
    /// </summary>
    public interface ILogEntry : IDataTransferObject
    {
        /// <summary>
        /// Gets a value indicating that this entry is a snapshot entry.
        /// </summary>
        bool IsSnapshot => false;

        /// <summary>
        /// Gets UTC time of the log entry when it was created.
        /// </summary>
        DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Obtains implementation-specific extension that can be used
        /// for performance optimizations by audit trail or log entry consumer.
        /// </summary>
        /// <typeparam name="TExtension">The type of the requested extension.</typeparam>
        /// <param name="extension">The requested extension.</param>
        /// <returns><see langword="true"/> if extension is supported; otherwise, <see langword="false"/>.</returns>
        /// <seealso cref="ContentLocationExtension"/>
        bool TryGetExtension<TExtension>(out TExtension extension)
            where TExtension : struct
        {
            extension = default;
            return false;
        }
    }
}