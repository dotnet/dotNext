using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using HeaderUtils = Microsoft.Net.Http.Headers.HeaderUtilities;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal abstract class RaftHttpMessage : HttpMessage
    {
        private protected static readonly ValueParser<DateTimeOffset> DateTimeParser = (string str, out DateTimeOffset value) => HeaderUtils.TryParseDate(str, out value);

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

        internal override void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(TermHeader, ConsensusTerm.ToString(InvariantCulture));
            base.PrepareRequest(request);
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
