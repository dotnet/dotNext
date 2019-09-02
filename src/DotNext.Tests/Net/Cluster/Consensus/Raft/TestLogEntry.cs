namespace DotNext.Net.Cluster.Consensus.Raft
{
    using TextMessage = Messaging.TextMessage;

    internal sealed class TestLogEntry : TextMessage, IRaftLogEntry
    {
        public TestLogEntry(string command)
            : base(command, "Entry")
        {
        }

        public long Term { get; set; }
    }
}