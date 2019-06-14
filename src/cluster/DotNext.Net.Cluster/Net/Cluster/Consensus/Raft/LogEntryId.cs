using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents identifier of the record in audit trail.
    /// </summary>
    public readonly struct LogEntryId : IEquatable<LogEntryId>
    {
        /// <summary>
        /// Intializes a new identifier of the log entry.
        /// </summary>
        /// <param name="term">Term when entry was received or created by leader.</param>
        /// <param name="index">Position of entry in the log.</param>
        public LogEntryId(long term, long index)
        {
            Term = term;
            Index = index;
        }

        /// <summary>
        /// Gets Term when entry was received or created by leader.
        /// </summary>
        public long Term { get; }

        /// <summary>
        /// Gets position of the entry in the log.
        /// </summary>
        public long Index { get; }
        
        /// <summary>
        /// Determines whether this identifier is equal to other identifier.
        /// </summary>
        /// <param name="other">The other identifier to be compared.</param>
        /// <returns><see langword="true"/> if both identifiers are equal; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in LogEntryId other)
            => Term == other.Term && Index == other.Index;

        bool IEquatable<LogEntryId>.Equals(LogEntryId other)
            => Equals(other);    

        /// <summary>
        /// Determines whether this identifier is equal to other identifier.
        /// </summary>
        /// <param name="other">The other identifier to be compared.</param>
        /// <returns><see langword="true"/> if both identifiers are equal; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
            => other is LogEntryId id && Equals(id);

        /// <summary>
        /// Computes hash code for this identifier.
        /// </summary>
        /// <returns>The hash code of this identifier.</returns>
        public override int GetHashCode() => (Term + Index).GetHashCode();

        /// <summary>
        /// Determines whether two identifiers are equal.
        /// </summary>
        /// <param name="first">The first identifier to compare.</param>
        /// <param name="second">The second identifier to compare.</param>
        /// <returns><see langword="true"/> if both identifiers are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in LogEntryId first, in LogEntryId second)
            => first.Equals(second);

        /// <summary>
        /// Determines whether two identifiers are not equal.
        /// </summary>
        /// <param name="first">The first identifier to compare.</param>
        /// <param name="second">The second identifier to compare.</param>
        /// <returns><see langword="false"/> if both identifiers are equal; otherwise, <see langword="true"/>.</returns>
        public static bool operator !=(in LogEntryId first, in LogEntryId second)
            => !first.Equals(second);
    }
}
