using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class MetadataMessage : HttpMessage, IHttpMessageReader<MemberMetadata>, IHttpMessageWriter<MemberMetadata>
{
    internal new const string MessageType = "Metadata";

    internal MetadataMessage(in ClusterMemberId sender)
        : base(MessageType, sender)
    {
    }

    private MetadataMessage(HeadersReader<StringValues> headers)
        : base(headers)
    {
    }

    internal MetadataMessage(HttpRequest request)
        : this(request.Headers.TryGetValue)
    {
    }

    async Task<MemberMetadata> IHttpMessageReader<MemberMetadata>.ParseResponse(HttpResponseMessage response, CancellationToken token)
    {
        var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
            return await JsonSerializer.DeserializeAsync<MemberMetadata>(stream, MemberMetadata.TypeInfo, token).ConfigureAwait(false) ?? new MemberMetadata();
    }

    public Task SaveResponse(HttpResponse response, MemberMetadata metadata, CancellationToken token)
    {
        response.StatusCode = StatusCodes.Status200OK;
        return JsonSerializer.SerializeAsync(response.Body, metadata, MemberMetadata.TypeInfo, token);
    }
}