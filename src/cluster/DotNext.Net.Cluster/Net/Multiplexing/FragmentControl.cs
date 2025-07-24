namespace DotNext.Net.Multiplexing;

internal enum FragmentControl : ushort
{
    DataChunk = 0,
    FinalDataChunk = 1,
    
    // system packets
    Heartbeat = 2,
}