namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal enum FlowControl : byte
    {
        None = 0,       //used for small RPC calls
        StreamStart = 0B_0001_0000,   //start of the packet stream
        Fragment = 0B_0010_0000,//sequence packet
        StreamEnd = 0B_0011_0000,   //the end of the stream transfer
        Ack = 0B_0101_0000,         //confirmation that datagram has been received
        Cancel = 0B_0110_0000,    //remote peer cancels the request
    }
}