namespace DotNext.Net.Cluster.DistributedServices
{
    /// <summary>
    /// Represents possible states of lifetime lease.
    /// </summary>
    internal enum LeaseState
    {
        Active = 0,
        Expired,
        Renewed
    }
}