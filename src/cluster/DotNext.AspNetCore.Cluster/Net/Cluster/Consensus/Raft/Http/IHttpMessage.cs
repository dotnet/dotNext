using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal interface IHttpMessage<Response>
    {
        Task<Response> ParseResponse(HttpResponseMessage response);

        Task SaveResponse(HttpResponse response, Response result);
    }
}