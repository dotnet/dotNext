using System;
using static System.Globalization.CultureInfo;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal abstract class RaftHttpMessage
    {
        //request - represents ID of sender node
        //response - represents ID of reply node
        private const string NodeIdHeader = "X-Raft-Node-Id";

        //request - represents Term value according with Raft protocol
        private const string TermHeader = "X-Raft-Term";

        //response - represents name of reply node
        private const string NodeNameHeader = "X-Raft-Node-Name";

        //request - represents request message type
        private const string MessageTypeHeader = "X-Raft-Message";

        internal readonly Guid MemberId;
        internal readonly long ConsensusTerm;
        private readonly string messageType;

        private protected RaftHttpMessage(string messageType, Guid memberId, long consensusTerm)
        {
            MemberId = memberId;
            this.ConsensusTerm = consensusTerm;
            this.messageType = messageType;
        }

        private protected RaftHttpMessage(HttpRequest request)
        {
            foreach (var header in request.Headers[NodeIdHeader])
                if (Guid.TryParse(header, out MemberId))
                    break;
            foreach (var header in request.Headers[TermHeader])
                if (long.TryParse(header, out ConsensusTerm))
                    break;
            messageType = GetMessageType(request);
        }

        internal static string GetMessageType(HttpRequest request) =>
            request.Headers[MessageTypeHeader].FirstOrDefault();

        private protected static string GetMemberName(HttpResponseMessage response)
            => response.Headers.GetValues(NodeNameHeader).FirstOrEmpty().OrThrow<RaftProtocolException>(() =>
                throw new RaftProtocolException(ExceptionMessages.MissingHeader(NodeNameHeader)));

        private protected static Guid GetMemberId(HttpResponseMessage response)
            => Guid.TryParse(response.Headers.GetValues(NodeIdHeader).FirstOrDefault(), out var id)
                ? id
                : throw new RaftProtocolException(ExceptionMessages.MissingHeader(NodeIdHeader));

        private protected virtual void FillRequest(HttpRequestMessage request)
        {
            request.Headers.Add(NodeIdHeader, MemberId.ToString());
            request.Headers.Add(TermHeader, Convert.ToString(ConsensusTerm, InvariantCulture));
            request.Headers.Add(MessageTypeHeader, messageType);
        }

        private protected static void FillResponse(HttpResponse response, Guid memberId, string memberName)
        {
            response.Headers.Add(NodeIdHeader, memberId.ToString());
            response.Headers.Add(NodeNameHeader, memberName);
        }

        public static explicit operator HttpRequestMessage(RaftHttpMessage message)
        {
            if (message is null)
                return null;
            var request = new HttpRequestMessage {Method = HttpMethod.Post};
            message.FillRequest(request);
            return request;
        }
    }

    internal abstract class RaftHttpMessage<TResponse> : RaftHttpMessage
    {
        internal delegate Task<TResponse> ResponseParser(HttpResponseMessage response);

        internal readonly struct Response
        {
            internal readonly string MemberName;
            internal readonly Guid MemberId;
            internal readonly TResponse Body;

            internal Response(HttpResponseMessage response, TResponse body)
            {
                Body = body;
                MemberName = GetMemberName(response);
                MemberId = GetMemberId(response);
            }
        }

        private protected RaftHttpMessage(string messageType, Guid memberId, long consensusTerm) 
            : base(messageType, memberId, consensusTerm)
        {
        }

        private protected RaftHttpMessage(HttpRequest request) : base(request)
        {
        }

        private protected static async Task<Response> GetResponse(HttpResponseMessage response, ResponseParser parser)
            => new Response(response, await parser(response).ConfigureAwait(false));
    }
}
