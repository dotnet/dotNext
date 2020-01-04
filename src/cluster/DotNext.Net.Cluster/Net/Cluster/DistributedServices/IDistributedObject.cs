using System;

namespace DotNext.Net.Cluster.DistributedServices
{
    internal interface IDistributedObject
    {
        /// <summary>
        /// Gets the owner of this distributed object.
        /// </summary>
        Guid Owner { get; }
    }
}