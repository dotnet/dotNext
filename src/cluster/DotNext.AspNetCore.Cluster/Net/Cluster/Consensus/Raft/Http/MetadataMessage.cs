using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class MetadataMessage : RaftHttpMessage<MemberMetadata>
    {
        internal const string MessageType = "Metadata";

        internal MetadataMessage(ILocalClusterMember sender)
            : base(MessageType, sender)
        {
        }

        private static async Task<MemberMetadata> ParseResponse(HttpResponseMessage response)
        {
            var serializer = new DataContractJsonSerializer(typeof(MemberMetadata));
            return (MemberMetadata) serializer.ReadObject(await response.Content.ReadAsStreamAsync()
                .ConfigureAwait(false));
        }

        internal static Task<Response> GetResponse(HttpResponseMessage response) => GetResponse(response, ParseResponse);

        internal static Task CreateResponse(HttpResponse response, ILocalClusterMember identity, MemberMetadata metadata)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            FillResponse(response, identity);
            var serializer = new DataContractJsonSerializer(typeof(MemberMetadata));
            serializer.WriteObject(response.Body, metadata);
            return Task.CompletedTask;
        }
    }
}
