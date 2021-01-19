using System;
using System.Net;
using System.Net.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class UnexpectedStatusCodeException : RaftProtocolException
    {
        private readonly Exception exception;
        internal readonly HttpStatusCode StatusCode;
        internal readonly string? Reason;

        internal UnexpectedStatusCodeException(HttpResponseMessage response, HttpRequestException e)
            : base(e.Message)
        {
            StatusCode = response.StatusCode;
            Reason = response.ReasonPhrase;
            exception = e;
        }

        internal UnexpectedStatusCodeException(NotImplementedException e)
            : base(e.Message)
        {
            StatusCode = HttpStatusCode.NotImplemented;
            Reason = string.Empty;
            exception = e;
        }

        public override Exception GetBaseException() => exception;
    }
}
