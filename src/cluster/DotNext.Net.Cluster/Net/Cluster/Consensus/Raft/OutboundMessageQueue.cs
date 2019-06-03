using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Threading.Tasks;

    internal sealed class MessageCompletionSource : CancelableTaskCompletionSource<IMessage>
    {
        private readonly IMessage message;

        internal MessageCompletionSource(IMessage message, ref CancellationToken token) : base(ref token) => this.message = message;

        internal IMessage Message => Task.IsCanceled ? null : message;

    }

    internal sealed class OutboundMessageQueue : LinkedList<MessageCompletionSource>
    {
        internal Task Enqueue(IMessage message, ref CancellationToken token)
        {
            var source = new MessageCompletionSource(message, ref token);
            AddLast(source);
            return source.Task;
        }

        internal void AbortAll(Exception e)
        {
            for(var current = First; !(current is null); current = current.Next, Remove(current))
            {
                current.Value.TrySetException(e);
                current.Value = null;
            }
        }

        internal void CancelAll()
        {
            for(var current = First; !(current is null); current = current.Next, Remove(current))
            {
                current.Value.Dispose();
                current.Value = null;
            }
        }
    }
}