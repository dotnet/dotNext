namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

internal enum MessageType : byte
{
    // request message types
    Vote = 0B_0000_0001,
    Resign = 0B_0000_0010,
    AppendEntries = 0B_0000_0100,
    InstallSnapshot = 0B_0000_0101,
    Metadata = 0B_0000_0110,
    PreVote = 0B_0000_0111,
    Configuration = 0B_0000_1000,
    Synchronize = 0B_0000_1001,

    // response message types
    None = 0,
}