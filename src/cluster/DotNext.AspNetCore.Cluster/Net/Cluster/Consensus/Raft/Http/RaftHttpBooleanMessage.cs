using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal abstract class RaftHttpBooleanMessage : RaftHttpMessage
    {
        private protected RaftHttpBooleanMessage(string messageType, IPEndPoint sender)
            : base(messageType, sender)
        {
        }

        private protected RaftHttpBooleanMessage(HttpRequest request) 
            : base(request)
        {
        }

        internal static async Task<bool> GetResponse(HttpResponseMessage response)
            => bool.TryParse(await response.Content.ReadAsStringAsync().ConfigureAwait(false), out var result)
                ? result
                : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

        internal static Task CreateResponse(HttpResponse response, bool result)
        {
            response.StatusCode = (int) HttpStatusCode.OK;
            return response.WriteAsync(Convert.ToString(result, InvariantCulture));
        }
    }
}
