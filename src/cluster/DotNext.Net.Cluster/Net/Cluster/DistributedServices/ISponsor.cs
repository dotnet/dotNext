using System;

namespace DotNext.Net.Cluster.DistributedServices
{
    internal interface ISponsor
    {
        bool IsAvailable(Guid owner);
    }
}