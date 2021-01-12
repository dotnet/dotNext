using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class MetadataMessage : HttpMessage, IHttpMessageReader<MemberMetadata>, IHttpMessageWriter<MemberMetadata>
    {
        internal new const string MessageType = "Metadata";

        internal MetadataMessage(IPEndPoint sender)
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
            var serializer = new DataContractJsonSerializer(typeof(MemberMetadata));
            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                return (MemberMetadata)serializer.ReadObject(stream);
        }

        public Task SaveResponse(HttpResponse response, MemberMetadata metadata, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status200OK;
            var serializer = new DataContractJsonSerializer(typeof(MemberMetadata));
            serializer.WriteObject(response.Body, metadata);
            return Task.CompletedTask;
        }
    }
}
