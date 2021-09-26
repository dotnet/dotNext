namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

/// <summary>
/// Represents network exchange between local and remote peer.
/// </summary>
internal interface IExchange
{
    /// <summary>
    /// Processes inbound packet received from the remote peer.
    /// </summary>
    /// <param name="headers">Received packet headers.</param>
    /// <param name="payload">Received packet payload.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> to call <see cref="CreateOutboundMessageAsync"/> afterwards and continues communication with remote peer; <see langword="false"/> to finalize communication.</returns>
    ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token);

    /// <summary>
    /// Creates a packet to send to the remote peer.
    /// </summary>
    /// <param name="buffer">The buffer containing outbound packet content.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> to wait for the inbound packet and subsequent call of <see cref="ProcessInboundMessageAsync"/>; <see langword="false"/> to finalize communication.</returns>
    ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> buffer, CancellationToken token);

    /// <summary>
    /// Notifies this exchange about exception occurred during processing of the packet.
    /// </summary>
    /// <param name="e">The exception caused by <see cref="ProcessInboundMessageAsync"/> or <see cref="CreateOutboundMessageAsync"/>.</param>
    void OnException(Exception e);

    /// <summary>
    /// Notifies this exchange about cancellation of communication.
    /// </summary>
    /// <param name="token">The token representing cancellation.</param>
    void OnCanceled(CancellationToken token);

    internal static int WriteResult(in Result<bool> result, Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);

        writer.WriteInt64(result.Term, true);
        writer.Add(result.Value.ToByte());

        return writer.WrittenCount;
    }

    internal static Result<bool> ReadResult(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        return new(reader.ReadInt64(true), ValueTypeExtensions.ToBoolean(reader.Read()));
    }
}