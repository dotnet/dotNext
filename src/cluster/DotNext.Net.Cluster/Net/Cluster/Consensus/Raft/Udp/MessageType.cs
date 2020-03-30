namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal enum MessageType : byte
    {
        Vote = 0,
        Resign = 0B_0000_0001,
        Heartbeat = 0B_0000_0010,
        AppendEntries = 0B_0000_0011,
        InstallSnapshot = 0B_0000_0100,
        Metadata = 0B_0000_0101
    }
}