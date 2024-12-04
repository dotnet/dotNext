using System.Buffers;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http;

using Buffers;
using IO;
using IO.Pipelines;

internal partial class HttpPeerController
{
    private const string DisconnectMessageType = "Disconnect";

    private async Task ProcessDisconnectAsync(HttpRequest request, HttpResponse response, int payloadLength, CancellationToken token)
    {
        EndPoint sender;
        bool isAlive;

        if (request.BodyReader.TryReadExactly(payloadLength, out var result))
        {
            (sender, isAlive) = DeserializeDisconnectRequest(result.Buffer, out var position);
            request.BodyReader.AdvanceTo(position);
        }
        else
        {
            using var buffer = allocator.AllocateExactly(payloadLength);
            await request.BodyReader.ReadExactlyAsync(buffer.Memory, token).ConfigureAwait(false);
            (sender, isAlive) = DeserializeDisconnectRequest(buffer.Memory);
        }

        await EnqueueDisconnectAsync(sender, isAlive, token).ConfigureAwait(false);
        response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static (EndPoint, bool) DeserializeDisconnectRequest(ref SequenceReader reader)
        => (reader.ReadEndPoint(), Unsafe.BitCast<byte, bool>(reader.ReadByte()));

    private static (EndPoint, bool) DeserializeDisconnectRequest(ReadOnlyMemory<byte> buffer)
    {
        var reader = IAsyncBinaryReader.Create(buffer);
        return DeserializeDisconnectRequest(ref reader);
    }

    private static (EndPoint, bool) DeserializeDisconnectRequest(ReadOnlySequence<byte> buffer, out SequencePosition position)
    {
        var reader = new SequenceReader(buffer);
        var result = DeserializeDisconnectRequest(ref reader);
        position = reader.Position;
        return result;
    }

    protected override async Task DisconnectAsync(EndPoint peer, bool isAlive, CancellationToken token)
    {
        using var request = SerializeDisconnectRequest(isAlive);
        await PostAsync(peer, DisconnectMessageType, request, token).ConfigureAwait(false);
    }

    private MemoryOwner<byte> SerializeDisconnectRequest(bool isAlive)
    {
        Debug.Assert(localNode is not null);

        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(64, allocator);

        try
        {
            writer.WriteEndPoint(localNode);
            writer.Add(Unsafe.BitCast<bool, byte>(isAlive));

            result = writer.DetachOrCopyBuffer();
        }
        finally
        {
            writer.Dispose();
        }

        return result;
    }
}