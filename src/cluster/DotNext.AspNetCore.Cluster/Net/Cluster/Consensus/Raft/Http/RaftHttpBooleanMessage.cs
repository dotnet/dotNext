using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal abstract class RaftHttpBooleanMessage : RaftHttpMessage<bool>
    {
        private protected RaftHttpBooleanMessage(string messageType, ILocalClusterMember sender)
            : base(messageType, sender)
        {
        }

        private protected RaftHttpBooleanMessage(HttpRequest request) 
            : base(request)
        {
        }

        private static async Task<bool> ParseResponse(HttpResponseMessage response)
            => bool.TryParse(await response.Content.ReadAsStringAsync().ConfigureAwait(false), out var result)
                ? result
                : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

        internal static Task<Response> GetResponse(HttpResponseMessage response) => GetResponse(response, ParseResponse);

        internal static Task CreateResponse(HttpResponse response, ILocalClusterMember identity, bool result)
        {
            response.StatusCode = (int) HttpStatusCode.OK;
            FillResponse(response, identity);
            return response.WriteAsync(Convert.ToString(result, InvariantCulture));
        }
    }
}
