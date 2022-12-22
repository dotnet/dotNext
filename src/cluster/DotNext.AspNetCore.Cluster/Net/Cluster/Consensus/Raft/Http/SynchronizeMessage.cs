using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Buffers.Binary.BinaryPrimitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Buffers;
using IO.Pipelines;
using static IO.StreamExtensions;

internal sealed class SynchronizeMessage : HttpMessage, IHttpMessageReader<long?>, IHttpMessageWriter<long?>
{
    internal new const string MessageType = "Synchronize";
    private const string CommitIndexHeader = "X-Raft-Commit-Index";

    internal readonly long CommitIndex;

    internal SynchronizeMessage(in ClusterMemberId sender, long commitIndex)
        : base(MessageType, in sender)
        => CommitIndex = commitIndex;

    private SynchronizeMessage(IDictionary<string, StringValues> headers)
        : base(headers)
        => CommitIndex = ParseHeader(headers, CommitIndexHeader, Int64Parser);

    internal SynchronizeMessage(HttpRequest request)
        : this(request.Headers)
    {
    }

    internal override void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(CommitIndexHeader, CommitIndex.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }

    async Task<long?> IHttpMessageReader<long?>.ParseResponse(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength == sizeof(long))
        {
            var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var buffer = MemoryAllocator.Allocate<byte>(sizeof(long), exactSize: true);
                await stream.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
                return ReadInt64LittleEndian(buffer.Span);
            }
        }

        return null;
    }

    public async Task SaveResponse(HttpResponse response, long? commitIndex, CancellationToken token)
    {
        response.StatusCode = StatusCodes.Status200OK;
        if (commitIndex.HasValue)
        {
            response.ContentLength = sizeof(long);
            response.ContentType = MediaTypeNames.Application.Octet;

            await response.StartAsync(token).ConfigureAwait(false);
            var result = await response.BodyWriter.WriteInt64Async(commitIndex.GetValueOrDefault(), true, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
        }
    }
}