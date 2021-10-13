namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

internal enum MessageType : byte
{
    // request message types
    Vote = 0B_0000_0001,
    Resign = 0B_0000_0010,
    Heartbeat = 0B_0000_0011,
    AppendEntries = 0B_0000_0100,
    InstallSnapshot = 0B_0000_0101,
    Metadata = 0B_0000_0110,
    PreVote = 0B_0000_0111,
    Configuration = 0B_0000_1000,
    Synchronize = 0B_0000_1001,

    // response message types
    None = 0,
    NextEntry = 0B_0000_1010,   // ask for the next record with the specified index
    Continue = 0B_0000_1011,    // ask for the next data chunk of the record
}