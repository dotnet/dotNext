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
    private const string ShuffleMessageType = "Shuffle";
    private const string ShuffleReplyMessageType = "ShuffleReply";

    private async Task ProcessShuffleReplyAsync(HttpRequest request, HttpResponse response, int payloadLength, CancellationToken token)
    {
        IReadOnlyCollection<EndPoint> peers;

        if (request.BodyReader.TryReadBlock(payloadLength, out var result))
        {
            peers = DeserializeShuffleReply(result.Buffer, out var position);
            request.BodyReader.AdvanceTo(position);
        }
        else
        {
            using var buffer = allocator.Invoke(payloadLength, true);
            await request.BodyReader.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
            peers = DeserializeShuffleReply(buffer.Memory);
        }

        await EnqueueShuffleReplyAsync(peers, token).ConfigureAwait(false);
        response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static IReadOnlyCollection<EndPoint> DeserializeShuffleReply(ref SequenceReader reader)
    {
        var count = reader.ReadInt32(true);
        var result = new List<EndPoint>(count);

        while (count-- > 0)
            result.Add(reader.ReadEndPoint());

        result.TrimExcess();
        return result;
    }

    private static IReadOnlyCollection<EndPoint> DeserializeShuffleReply(ReadOnlyMemory<byte> buffer)
    {
        var reader = IAsyncBinaryReader.Create(buffer);
        return DeserializeShuffleReply(ref reader);
    }

    private static IReadOnlyCollection<EndPoint> DeserializeShuffleReply(ReadOnlySequence<byte> buffer, out SequencePosition position)
    {
        var reader = IAsyncBinaryReader.Create(buffer);
        var result = DeserializeShuffleReply(ref reader);
        position = reader.Position;
        return result;
    }

    protected override async Task ShuffleReplyAsync(EndPoint receiver, IReadOnlyCollection<EndPoint> peers, CancellationToken token = default)
    {
        using var request = SerializeShuffleReply(peers);
        await PostAsync(receiver, ShuffleReplyMessageType, request, token).ConfigureAwait(false);
    }

    private MemoryOwner<byte> SerializeShuffleReply(IReadOnlyCollection<EndPoint> peers)
    {
        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(256, allocator);

        try
        {
            writer.WriteInt32(peers.Count, true);
            foreach (var peer in peers)
                writer.WriteEndPoint(peer);

            if (!writer.TryDetachBuffer(out result))
                result = writer.WrittenSpan.Copy(allocator);
        }
        finally
        {
            writer.Dispose();
        }

        return result;
    }

    private async Task ProcessShuffleRequestAsync(HttpRequest request, HttpResponse response, long payloadLength, CancellationToken token)
    {
        EndPoint sender, origin;
        IReadOnlyCollection<EndPoint> peers;
        int timeToLive;

        if (request.BodyReader.TryReadBlock(payloadLength, out var result))
        {
            (sender, origin, timeToLive, peers) = DeserializeShuffleRequest(result.Buffer, out var position);
            request.BodyReader.AdvanceTo(position);
        }
        else
        {
            using var buffer = new PooledBufferWriter<byte>
            {
                BufferAllocator = allocator,
                Capacity = payloadLength.Truncate()
            };

            await request.BodyReader.CopyToAsync(buffer, token).ConfigureAwait(false);
            (sender, origin, timeToLive, peers) = DeserializeShuffleRequest(buffer.WrittenMemory);
        }

        await EnqueueShuffleAsync(sender, origin, peers, timeToLive, token).ConfigureAwait(false);
        response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static (EndPoint, EndPoint, int, IReadOnlyCollection<EndPoint>) DeserializeShuffleRequest(ref SequenceReader reader)
    {
        var sender = reader.ReadEndPoint();
        var origin = reader.ReadEndPoint();
        var timeToLive = reader.ReadInt32(true);

        var count = reader.ReadInt32(true);
        var peers = new List<EndPoint>(count);

        while (count-- > 0)
            peers.Add(reader.ReadEndPoint());

        peers.TrimExcess();
        return (sender, origin, timeToLive, peers);
    }

    private static (EndPoint, EndPoint, int, IReadOnlyCollection<EndPoint>) DeserializeShuffleRequest(ReadOnlyMemory<byte> buffer)
    {
        var reader = IAsyncBinaryReader.Create(buffer);
        return DeserializeShuffleRequest(ref reader);
    }

    private static (EndPoint, EndPoint, int, IReadOnlyCollection<EndPoint>) DeserializeShuffleRequest(ReadOnlySequence<byte> buffer, out SequencePosition position)
    {
        var reader = IAsyncBinaryReader.Create(buffer);
        var result = DeserializeShuffleRequest(ref reader);
        position = reader.Position;
        return result;
    }

    protected sealed override async Task ShuffleAsync(EndPoint receiver, EndPoint? origin, IReadOnlyCollection<EndPoint> peers, int timeToLive, CancellationToken token = default)
    {
        Debug.Assert(localNode is not null);

        using var request = SerializeShuffleRequest(origin ?? localNode, peers, timeToLive);
        await PostAsync(receiver, ShuffleMessageType, request, token).ConfigureAwait(false);
    }

    private MemoryOwner<byte> SerializeShuffleRequest(EndPoint origin, IReadOnlyCollection<EndPoint> peers, int timeToLive)
    {
        Debug.Assert(localNode is not null);

        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(256, allocator);

        try
        {
            writer.WriteEndPoint(localNode);
            writer.WriteEndPoint(origin);
            writer.WriteInt32(timeToLive, true);

            writer.WriteInt32(peers.Count, true);
            foreach (var peer in peers)
                writer.WriteEndPoint(peer);

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