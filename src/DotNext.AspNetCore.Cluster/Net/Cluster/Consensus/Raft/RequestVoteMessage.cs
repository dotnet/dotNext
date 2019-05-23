using System;
using static System.Globalization.CultureInfo;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RequestVoteMessage : RaftHttpMessage<bool>
    {
        internal const string MessageType = "Vote";

        internal RequestVoteMessage(Guid memberId, long consensusTerm)
            : base(MessageType, memberId, consensusTerm)
        {
        }

        internal RequestVoteMessage(HttpRequest request)
            : base(request)
        {
        }

        private static async Task<bool> ParseResponse(HttpResponseMessage response)
            => bool.TryParse(await response.Content.ReadAsStringAsync().ConfigureAwait(false), out var result)
                ? result
                : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

        internal static Task<Response> GetResponse(HttpResponseMessage response) => GetResponse(response, ParseResponse);

        internal static Task CreateResponse(HttpResponse response, Guid memberId, string memberName, bool vote)
        {
            FillResponse(response, memberId, memberName);
            return response.WriteAsync(Convert.ToString(vote, InvariantCulture));
        }
    }
}
