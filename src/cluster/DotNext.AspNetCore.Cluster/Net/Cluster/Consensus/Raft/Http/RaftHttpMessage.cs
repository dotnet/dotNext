using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Net.Http;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal abstract class RaftHttpMessage : HttpMessage
    {
        //request - represents Term value according with Raft protocol
        private const string TermHeader = "X-Raft-Term";
        
        internal readonly long ConsensusTerm;

        private protected RaftHttpMessage(string messageType, IPEndPoint sender, long term) : base(messageType, sender) => ConsensusTerm = term;

        private protected RaftHttpMessage(HttpRequest request)
            : base(request)
        {
            foreach (var header in request.Headers[TermHeader])
                if (long.TryParse(header, out ConsensusTerm))
                    break;
        }

        private protected override void FillRequest(HttpRequestMessage request)
        {
            request.Headers.Add(TermHeader, Convert.ToString(ConsensusTerm, InvariantCulture));
            base.FillRequest(request);
        }

        private protected static LogEntryId? ParseLogEntryId(HttpRequest request, string indexHeader, string termHeader)
        {
            long? term = null, index = null;
            foreach (var header in request.Headers[indexHeader])
                if (long.TryParse(header, out var value))
                {
                    index = value;
                    break;
                }

            foreach (var header in request.Headers[termHeader])
                if (long.TryParse(header, out var value))
                {
                    term = value;
                    break;
                }

            return term is null || index is null ? default(LogEntryId?) : new LogEntryId(term.Value, index.Value);
        }
    }
}
