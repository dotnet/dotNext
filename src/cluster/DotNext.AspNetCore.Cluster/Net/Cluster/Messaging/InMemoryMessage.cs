using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging
{
    internal sealed class InMemoryMessage : StreamMessage, IBufferedMessage
    {
        internal InMemoryMessage(string name, ContentType type, int size)
            : base(name, type, size, false)
        {
        }

        void IBufferedMessage.PrepareForReuse()
        {
        }
    }
}