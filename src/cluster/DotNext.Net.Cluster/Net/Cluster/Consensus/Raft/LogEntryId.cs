using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents identifier of the record in audit trail.
    /// </summary>
    public readonly struct LogEntryId : IEquatable<LogEntryId>
    {
        public LogEntryId(long term, long index)
        {
            Term = term;
            Index = index;
        }

        public long Term { get; }
        public long Index { get; }

        [CLSCompliant(false)]
        public bool Equals(in LogEntryId other)
            => Term == other.Term && Index == other.Index;

        bool IEquatable<LogEntryId>.Equals(LogEntryId other)
            => Equals(other);    

        public override bool Equals(object other)
            => other is LogEntryId id && Equals(id);

        public override int GetHashCode() => (Term + Index).GetHashCode();

        public static bool operator ==(in LogEntryId first, in LogEntryId second)
            => first.Equals(second);

        public static bool operator !=(in LogEntryId first, in LogEntryId second)
            => !first.Equals(second);
    }
}
