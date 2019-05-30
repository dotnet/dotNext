using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RaftHttpCluster : RaftCluster, IMiddleware, IHostedService, IRaftLocalMember
    {
        internal RaftHttpCluster(IOptions<RaftClusterMemberConfiguration> config)
            : base(config.Value)
        {
        }

        long IRaftLocalMember.Term => Term;

        bool IRaftLocalMember.IsLeader(IClusterMember member) => ReferenceEquals(Leader, member);

        private async Task Vote(RequestVoteMessage request, HttpResponse response)
            => await RequestVoteMessage.CreateResponse(response, this, await Vote(request, request.ConsensusTerm).ConfigureAwait(false))
                .ConfigureAwait(false);

        private async Task Resign(HttpResponse response) =>
            await ResignMessage.CreateResponse(response, this, await Resign().ConfigureAwait(false))
                .ConfigureAwait(false);

        private async Task ReceiveAppendEntries(RaftHttpMessage request, HttpResponse response)
        {
            if(request.MemberId == id)  //sender node and receiver are same, ignore message
                return;
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            switch (RaftHttpMessage.GetMessageType(context.Request))
            {
                case RequestVoteMessage.MessageType:
                    return Vote(new RequestVoteMessage(context.Request),  context.Response);
                case ResignMessage.MessageType:
                    return Resign(context.Response);
                default:
                    return next(context);
            }
        }
    }
}
