namespace DotNext.Net.Cluster.DistributedServices
{
    internal interface ISponsor<TObject>
        where TObject : IDistributedObject
    {
        LeaseState UpdateLease(ref TObject obj);
    }
}