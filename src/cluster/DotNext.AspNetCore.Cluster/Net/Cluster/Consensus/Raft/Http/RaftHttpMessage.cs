using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;
using HeaderUtils = Microsoft.Net.Http.Headers.HeaderUtilities;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal abstract class RaftHttpMessage : HttpMessage
    {
        // cached to avoid memory allocation
        private protected static readonly ValueParser<DateTimeOffset> Rfc1123Parser = TryParseRfc1123FormattedDateTime;

        // request - represents Term value according with Raft protocol
        // response - represents Term value of the reply node
        private const string TermHeader = "X-Raft-Term";

        internal readonly long ConsensusTerm;

        private protected RaftHttpMessage(string messageType, in ClusterMemberId sender, long term)
            : base(messageType, sender) => ConsensusTerm = term;

        private protected RaftHttpMessage(HeadersReader<StringValues> headers)
            : base(headers)
        {
            ConsensusTerm = ParseHeader(TermHeader, headers, Int64Parser);
        }

        internal sealed override bool IsMemberUnavailable(HttpStatusCode? code) => true;

        internal override void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(TermHeader, ConsensusTerm.ToString(InvariantCulture));
            base.PrepareRequest(request);
        }

        private static bool TryParseRfc1123FormattedDateTime(string input, out DateTimeOffset result)
            => HeaderUtils.TryParseDate(input, out result);

        private protected static new async Task<Result<bool>> ParseBoolResponse(HttpResponseMessage response, CancellationToken token)
        {
            var result = await HttpMessage.ParseBoolResponse(response, token).ConfigureAwait(false);
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
