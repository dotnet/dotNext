namespace DotNext.Net.Cluster.DistributedServices
{
    /// <summary>
    /// Provides sponsorship for the given distributed object.
    /// </summary>
    /// <param name="obj">The distributed object.</param>
    /// <typeparam name="TObject">The type of distributed object.</typeparam>
    /// <returns>The lease state of distributed object.</returns>
    internal delegate LeaseState Sponsor<TObject>(ref TObject obj)
        where TObject : IDistributedObject;
    
    internal static class Sponsor
    {
        internal static LeaseState Renewal(this ISponsor sponsor, ref DistributedLock lockInfo)
        {
            if(lockInfo.IsExpired)
                return LeaseState.Expired;
            if(sponsor.IsAvailable(lockInfo.Owner))
            {
                lockInfo.Renew();
                return LeaseState.Renewed;
            }
            return LeaseState.Active;
        }
    }
}