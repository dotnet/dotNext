using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Buffers;
using static IO.StreamExtensions;

internal sealed class SynchronizeMessage : HttpMessage, IHttpMessageReader<long?>, IHttpMessageWriter<long?>
{
    internal new const string MessageType = "Synchronize";

    internal SynchronizeMessage(in ClusterMemberId sender)
        : base(MessageType, in sender)
    {
    }

    private SynchronizeMessage(HeadersReader<StringValues> headers)
        : base(headers)
    {
    }

    internal SynchronizeMessage(HttpRequest request)
        : this(request.Headers.TryGetValue)
    {
    }

    async Task<long?> IHttpMessageReader<long?>.ParseResponse(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength == sizeof(long))
        {
            using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var buffer = MemoryAllocator.Allocate<byte>(sizeof(long), exactSize: true);
            await stream.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
            return ReadInt64LittleEndian(buffer.Memory.Span);
        }

        return null;
    }

    public Task SaveResponse(HttpResponse response, long? commitIndex, CancellationToken token)
    {
        response.StatusCode = StatusCodes.Status200OK;
        if (commitIndex.HasValue)
        {
            response.ContentLength = sizeof(long);
            response.ContentType = MediaTypeNames.Application.Octet;
            var buffer = response.BodyWriter.GetSpan(sizeof(long));
            WriteInt64LittleEndian(buffer, commitIndex.GetValueOrDefault());
            response.BodyWriter.Advance(sizeof(long));
        }

        return Task.CompletedTask;
    }
}