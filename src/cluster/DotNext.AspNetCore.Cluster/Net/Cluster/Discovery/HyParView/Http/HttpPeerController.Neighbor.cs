using System.Buffers;
using System.Net;
using Microsoft.AspNetCore.Http;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http;

using Buffers;
using IO;
using IO.Pipelines;

internal partial class HttpPeerController
{
    private const string NeighborMessageType = "Neighbor";

    private async Task ProcessNeighborAsync(HttpRequest request, HttpResponse response, int payloadLength, CancellationToken token)
    {
        EndPoint sender;
        bool highPriority;

        if (request.BodyReader.TryReadBlock(payloadLength, out var result))
        {
            // fast path, no need to allocate temp buffer
            (sender, highPriority) = DeserializeNeighborRequest(result.Buffer, out var position);
            request.BodyReader.AdvanceTo(position);
        }
        else
        {
            // slow path, allocate temp buffer
            using var buffer = allocator.Invoke(payloadLength, true);
            await request.BodyReader.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
            (sender, highPriority) = DeserializeNeighborRequest(buffer.Memory);
        }

        await EnqueueNeighborAsync(sender, highPriority, token).ConfigureAwait(false);
        response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static (EndPoint, bool) DeserializeNeighborRequest(ref SequenceReader reader)
        => (reader.ReadEndPoint(), ValueTypeExtensions.ToBoolean(reader.Read<byte>()));

    private static (EndPoint, bool) DeserializeNeighborRequest(ReadOnlyMemory<byte> buffer)
    {
        var reader = IAsyncBinaryReader.Create(buffer);
        return DeserializeNeighborRequest(ref reader);
    }

    private static (EndPoint, bool) DeserializeNeighborRequest(ReadOnlySequence<byte> buffer, out SequencePosition position)
    {
        var reader = IAsyncBinaryReader.Create(buffer);
        var result = DeserializeNeighborRequest(ref reader);
        position = reader.Position;
        return result;
    }

    protected sealed override async Task NeighborAsync(EndPoint neighbor, bool highPriority, CancellationToken token)
    {
        using var request = SerializeNeighborRequest(highPriority);
        await PostAsync(neighbor, NeighborMessageType, request, token).ConfigureAwait(false);
    }

    private MemoryOwner<byte> SerializeNeighborRequest(bool highPriority)
    {
        Debug.Assert(localNode is not null);

        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(64, allocator);

        try
        {
            writer.WriteEndPoint(localNode);
            writer.Add(highPriority.ToByte());

            if (!writer.TryDetachBuffer(out result))
                result = writer.WrittenSpan.Copy(allocator);
        }
        finally
        {
            writer.Dispose();
        }

        return result;
    }
}