using System.IO.Pipelines;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using static IO.DataTransferObject;
using static IO.Pipelines.PipeExtensions;
using static IO.Pipelines.ResultExtensions;

internal partial class ServerExchange
{
    private void BeginSendMetadata(CancellationToken token)
    {
        task = SerializeAsync(new MetadataTransferObject(server.Metadata), Writer, token);

        static async Task SerializeAsync(MetadataTransferObject metadata, PipeWriter writer, CancellationToken token)
        {
            await metadata.WriteToAsync(writer, token).ConfigureAwait(false);
            await writer.CompleteAsync().ConfigureAwait(false);
        }
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