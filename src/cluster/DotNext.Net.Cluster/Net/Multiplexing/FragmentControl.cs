namespace DotNext.Net.Multiplexing;

/// <summary>
/// Controls the fragment behavior.
/// </summary>
internal enum FragmentControl : ushort
{
    /// <summary>
    /// The fragment contains a data chunk.
    /// </summary>
    DataChunk = 0,
    
    /// <summary>
    /// The fragment contains a final data chunk for the particular stream.
    /// </summary>
    /// <remarks>
    /// Every stream can receive this type of the fragment just once per its lifetime.
    /// </remarks>
    FinalDataChunk = 1,
    
    /// <summary>
    /// The stream is rejected by the server due to backlog overflow.
    /// </summary>
    /// <remarks>
    /// This fragment has no data, only the header.
    /// </remarks>
    StreamRejected = 2,
    
    /// <summary>
    /// The stream is closed.
    /// </summary>
    StreamClosed = 3,
    
    // System packets. All of them must have stream ID = 0
    
    /// <summary>
    /// Heartbeat system fragment.
    /// </summary>
    Heartbeat = 1024,
}