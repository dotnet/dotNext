using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Messaging;

    /// <summary>
    /// Represents log entry in Raft audit trail.
    /// </summary>
    public readonly struct LogEntry
    {
        public readonly long Index;
        public readonly long Term;
        public readonly IMessage Command;

        public LogEntry(long index, long term, IMessage command)
        {
            Index = index;
            Term = term;
            Command = command;
        }
    }
}