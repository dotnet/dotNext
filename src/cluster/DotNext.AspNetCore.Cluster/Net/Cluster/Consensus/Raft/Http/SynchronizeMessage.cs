using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Buffers.Binary.BinaryPrimitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Buffers;
using IO.Pipelines;
using static IO.StreamExtensions;

internal sealed class SynchronizeMessage : HttpMessage, IHttpMessage<long?>
{
    internal const string MessageType = "Synchronize";
    private const string CommitIndexHeader = "X-Raft-Commit-Index";

    internal readonly long CommitIndex;

    internal SynchronizeMessage(in ClusterMemberId sender, long commitIndex)
        : base(in sender)
        => CommitIndex = commitIndex;

    private SynchronizeMessage(IDictionary<string, StringValues> headers)
        : base(headers)
        => CommitIndex = ParseHeader(headers, CommitIndexHeader, Int64Parser);

    internal SynchronizeMessage(HttpRequest request)
        : this(request.Headers)
    {
    }

    public new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(CommitIndexHeader, CommitIndex.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }

    Task<long?> IHttpMessage<long?>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token)
    {
        return response.Content.Headers.ContentLength is sizeof(long)
            ? ParseAsync(response.Content, token)
            : Task.FromResult<long?>(null);

        static async Task<long?> ParseAsync(HttpContent content, CancellationToken token)
        {
            var stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var buffer = MemoryAllocator.AllocateExactly<byte>(sizeof(long));
                await stream.ReadExactlyAsync(buffer.Memory, token).ConfigureAwait(false);
                return ReadInt64LittleEndian(buffer.Span);
            }
        }
    }

    static string IHttpMessage.MessageType => MessageType;

    internal static Task SaveResponseAsync(HttpResponse response, long? commitIndex, CancellationToken token)
    {
        Task result;

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = MediaTypeNames.Application.Octet;
        if (commitIndex.HasValue)
        {
            response.ContentLength = sizeof(long);
            result = SaveAsync(response, commitIndex.GetValueOrDefault(), token);
        }
        else
        {
            response.ContentLength = 0L;
            result = Task.CompletedTask;
        }

        return result;

        static async Task SaveAsync(HttpResponse response, long commitIndex, CancellationToken token)
        {
            await response.StartAsync(token).ConfigureAwait(false);
            var result = await response.BodyWriter.WriteInt64Async(commitIndex, littleEndian: true, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
        }
    }
}