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
}