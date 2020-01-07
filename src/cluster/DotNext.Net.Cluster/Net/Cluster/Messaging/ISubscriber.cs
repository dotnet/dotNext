namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents cluster member that supports messaging.
    /// </summary>
    public interface ISubscriber : IClusterMember, IOutputChannel
    {
    }
}