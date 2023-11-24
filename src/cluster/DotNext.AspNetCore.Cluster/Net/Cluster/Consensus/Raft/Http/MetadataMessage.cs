using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class MetadataMessage : HttpMessage, IHttpMessage<MemberMetadata>
{
    internal const string MessageType = "Metadata";

    internal MetadataMessage(in ClusterMemberId sender)
        : base(sender)
    {
    }

    internal MetadataMessage(HttpRequest request)
        : base(request.Headers)
    {
    }

    Task<MemberMetadata> IHttpMessage<MemberMetadata>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token)
    {
        return ParseAsync(response.Content, token);

        static async Task<MemberMetadata> ParseAsync(HttpContent content, CancellationToken token)
        {
            var stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
            try
            {
                return await JsonSerializer.DeserializeAsync<MemberMetadata>(stream, MemberMetadata.TypeInfo, token).ConfigureAwait(false) ?? new MemberMetadata();
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    void IHttpMessage.PrepareRequest(HttpRequestMessage request) => PrepareRequest(request);

    static string IHttpMessage.MessageType => MessageType;

    internal static Task SaveResponseAsync(HttpResponse response, MemberMetadata metadata, CancellationToken token)
    {
        response.StatusCode = StatusCodes.Status200OK;
        return JsonSerializer.SerializeAsync(response.Body, metadata, MemberMetadata.TypeInfo, token);
    }
}