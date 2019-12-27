using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    internal interface IDistributedServiceProvider
    {
        Task InitializeAsync(CancellationToken token);
    }
}