using System;
using static System.Globalization.CultureInfo;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal abstract class RaftHttpMessage
    {
        //request - represents IP of sender node
        private const string NodeIpHeader = "X-Raft-Node-IP";
        //request - represents hosting port of sender node
        private const string NodePortHeader = "X-Raft-Node-Port";

        //request - represents Term value according with Raft protocol
        private const string TermHeader = "X-Raft-Term";

        //request - represents request message type
        private const string MessageTypeHeader = "X-Raft-Message";

        internal readonly IPEndPoint Sender;
        private readonly string messageType;
        internal readonly long ConsensusTerm;

        private protected RaftHttpMessage(string messageType, IPEndPoint sender)
        {
            Sender = sender;
            this.messageType = messageType;
        }

        private protected RaftHttpMessage(HttpRequest request)
        {
            var address = default(IPAddress);
            var port = 0;
            foreach (var header in request.Headers[NodeIpHeader])
                if (IPAddress.TryParse(header, out address))
                    break;
            foreach (var header in request.Headers[NodePortHeader])
                if (int.TryParse(header, out port))
                    break;
            Sender = new IPEndPoint(address ?? throw new RaftProtocolException(ExceptionMessages.MissingHeader(NodeIpHeader)), port);
            foreach (var header in request.Headers[TermHeader])
                if (long.TryParse(header, out ConsensusTerm))
                    break;
            messageType = GetMessageType(request);
        }

        internal static string GetMessageType(HttpRequest request) =>
            request.Headers[MessageTypeHeader].FirstOrDefault() ??
            throw new RaftProtocolException(ExceptionMessages.MissingHeader(MessageTypeHeader));

        private protected virtual void FillRequest(HttpRequestMessage request)
        {
            request.Headers.Add(NodeIpHeader, Sender.Address.ToString());
            request.Headers.Add(NodePortHeader, Convert.ToString(Sender.Port, InvariantCulture));
            request.Headers.Add(TermHeader, Convert.ToString(ConsensusTerm, InvariantCulture));
            request.Headers.Add(MessageTypeHeader, messageType);
            request.Method = HttpMethod.Post;
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
        private protected RaftHttpMessage(string messageType, IPEndPoint sender) 
            : base(messageType, sender)
        {
        }

        private protected RaftHttpMessage(HttpRequest request) : base(request)
        {
        }
    }
}
