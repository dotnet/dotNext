using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftResponder : RaftClusterNode
    {
        private readonly RequestDelegate nextMiddleware;

        internal RaftResponder(RequestDelegate nextMiddleware)
        {
            this.nextMiddleware = nextMiddleware;
        }

        private Task MakeResponse(HttpContext context)
        {
            
        }

        public static implicit operator RequestDelegate(RaftResponder responder)
            => responder is null ? default(RequestDelegate) : responder.MakeResponse;
    }
}
