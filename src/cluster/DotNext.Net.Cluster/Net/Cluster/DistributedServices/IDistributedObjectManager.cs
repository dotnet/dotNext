using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    internal interface IDistributedObjectManager<TObject>
        where TObject : IDistributedObject
    {
         //releases all expired objects
        Task ProvideSponsorshipAsync<TSponsor>(TSponsor sponsor, CancellationToken token)
            where TSponsor : ISponsor<TObject>;
    }
}