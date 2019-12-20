using System;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents a message that should be disposed when no longer needed.
    /// </summary>
    public interface IDisposableMessage : IMessage, IDisposable
    {
    }
}