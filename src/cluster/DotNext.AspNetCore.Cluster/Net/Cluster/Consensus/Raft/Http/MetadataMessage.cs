using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class MetadataMessage : HttpMessage, IHttpMessageReader<MemberMetadata>, IHttpMessageWriter<MemberMetadata>
    {
        private const JsonSerializerOptions? JsonOptions = null;
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
#if NETCOREAPP3_1
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
#endif
            return await JsonSerializer.DeserializeAsync<MemberMetadata>(stream, JsonOptions, token).ConfigureAwait(false) ?? new MemberMetadata();
        }

        public Task SaveResponse(HttpResponse response, MemberMetadata metadata, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status200OK;
            return JsonSerializer.SerializeAsync(response.Body, metadata, JsonOptions, token);
        }
    }
}
