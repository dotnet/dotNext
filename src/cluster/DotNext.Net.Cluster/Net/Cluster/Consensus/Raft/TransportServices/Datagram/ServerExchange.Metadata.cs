namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using static IO.Pipelines.PipeExtensions;
using DataTransferObject = IO.DataTransferObject;

internal partial class ServerExchange
{
    private void BeginSendMetadata(CancellationToken token)
    {
        task = DataTransferObject.WriteToAsync(new MetadataTransferObject(server.Metadata), Writer, token).AsTask();
    }

    private async ValueTask<(PacketHeaders, int, bool)> SendMetadataPortionAsync(bool startStream, Memory<byte> output, CancellationToken token)
    {
        bool continueSending;
        FlowControl control;
        var bytesWritten = await Reader.CopyToAsync(output, token).ConfigureAwait(false);

        // final packet detected
        if (bytesWritten == output.Length)
        {
            control = startStream ? FlowControl.StreamStart : FlowControl.Fragment;
            continueSending = true;
        }
        else
        {
            control = FlowControl.StreamEnd;
            continueSending = false;
        }

        return (new PacketHeaders(MessageType.Metadata, control), bytesWritten, continueSending);
    }
}