using System.Diagnostics.Tracing;

namespace RaftNode
{
    // for application metrics
    internal sealed class AppEventSource : EventSource
    {
        public AppEventSource()
            : base("RaftNode.Events")
        {
        }
    }
}