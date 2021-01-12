using System;
using System.Net;
using System.Net.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class UnexpectedStatusCodeException : RaftProtocolException
    {
        private readonly HttpRequestException exception;
        internal readonly HttpStatusCode StatusCode;
        internal readonly string Reason;

        internal UnexpectedStatusCodeException(HttpResponseMessage response, HttpRequestException e)
            : base(e.Message)
        {
            StatusCode = response.StatusCode;
            Reason = response.ReasonPhrase;
            exception = e;
        }

        public override Exception GetBaseException() => exception;
    }
}
