using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Net.Cluster.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using ContentDispositionHeaderValue = System.Net.Http.Headers.ContentDispositionHeaderValue;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Collections.Generic;

    internal sealed class AppendEntriesMessage : RaftHttpBooleanMessage
    {
        internal const string MessageType = "AppendEntries";

        private sealed class MessageContent : HttpContent
        {
            private readonly IMessage message;

            internal MessageContent(IMessage message)
            {
                this.message = message;
                Headers.ContentType = MediaTypeHeaderValue.Parse(message.Type.ToString());
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => message.CopyToAsync(stream);

            protected override bool TryComputeLength(out long length) => message.Length.TryGet(out length);
        }

        internal AppendEntriesMessage(IPEndPoint sender)
            : base(MessageType, sender)
        {

        }

        internal IMessage Message { get; set; }

        private protected override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            var message = Message;
            if (!(message is null))
                request.Content = new MessageContent(message);
        }
    }
}