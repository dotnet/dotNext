using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        internal override ValueTask FillRequestAsync(HttpRequestMessage request)
        {
            request.Headers.Add(TermHeader, ConsensusTerm.ToString(InvariantCulture));
            return base.FillRequestAsync(request);
        }

        private protected static new async Task<Result<bool>> ParseBoolResponse(HttpResponseMessage response)
        {
            var result = await HttpMessage.ParseBoolResponse(response).ConfigureAwait(false);
            var term = ParseHeader<IEnumerable<string>, long>(TermHeader, response.Headers.TryGetValues, Int64Parser);
            return new Result<bool>(term, result);
        }

        private protected static Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status200OK;
            response.Headers.Add(TermHeader, result.Term.ToString(InvariantCulture));
            return response.WriteAsync(result.Value.ToString(InvariantCulture), token);
        }
    }
}
