namespace DotNext.Net.Cluster.DistributedServices
{
    internal interface IDistributedObject
    {
        /// <summary>
        /// Gets the owner of this distributed object.
        /// </summary>
        ClusterMemberId Owner { get; }
    }
}