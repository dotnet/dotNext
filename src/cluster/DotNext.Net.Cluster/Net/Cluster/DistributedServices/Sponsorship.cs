namespace DotNext.Net.Cluster.DistributedServices
{
    internal static class Sponsorship
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