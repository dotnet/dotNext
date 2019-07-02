using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal abstract class RaftHttpMessage : HttpMessage
    {

        //request - represents Term value according with Raft protocol
        //response - represents Term value of the reply node
        private const string TermHeader = "X-Raft-Term";
        
        internal readonly long ConsensusTerm;

        private protected RaftHttpMessage(string messageType, IPEndPoint sender, long term) : base(messageType, sender) => ConsensusTerm = term;

        private protected RaftHttpMessage(HeadersReader<StringValues> headers)
            : base(headers)
        {
            ConsensusTerm = ParseHeader(TermHeader, headers, Int64Parser);
        }

        private protected override void FillRequest(HttpRequestMessage request)
        {
            request.Headers.Add(TermHeader, Convert.ToString(ConsensusTerm, InvariantCulture));
            base.FillRequest(request);
        }

        private protected new static async Task<Result<bool>> ParseBoolResponse(HttpResponseMessage response)
        {
            var result = await HttpMessage.ParseBoolResponse(response).ConfigureAwait(false);
            var term = ParseHeader<IEnumerable<string>, long>(TermHeader, response.Headers.TryGetValues, Int64Parser);
            return new Result<bool>(term, result);
        }

        private protected static Task SaveResponse(HttpResponse response, Result<bool> result)
        {
            response.StatusCode = StatusCodes.Status200OK;
            response.Headers.Add(TermHeader, Convert.ToString(result.Term, InvariantCulture));
            return response.WriteAsync(Convert.ToString(result.Value, InvariantCulture));
        }
    }
}
