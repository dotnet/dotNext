using System;

namespace DotNext.Net.Cluster.Replication
{
    using Messaging;

    /// <summary>
    /// Represents log entry in the form of replication message.
    /// </summary>
    /// <typeparam name="EntryId">The type representing unique identifier of log entry.</typeparam>
    public interface ILogEntry<EntryId> : IMessage
        where EntryId : struct, IEquatable<EntryId>
    {
        /// <summary>
        /// Gets identifier of this log entry.
        /// </summary>
        ref readonly EntryId Id { get; }
    }
}
