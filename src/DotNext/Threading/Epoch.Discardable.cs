namespace DotNext.Threading;

using Runtime.ExceptionServices;

partial class Epoch
{
    /// <summary>
    /// Represents an object which lifetime is controlled by <see cref="Epoch"/> internal Garbage Collector.
    /// </summary>
    public abstract class Discardable : IThreadPoolWorkItem
    {
        private Discardable? next;

        /// <summary>
        /// Automatically called by <see cref="Epoch"/> infrastructure to clean up resources associated with this object.
        /// </summary>
        protected abstract void Discard();

        internal void Drain()
        {
            for (Discardable? current = this, nextNode; current is not null; current = nextNode)
            {
                nextNode = current.next;
                current.next = null; // help GC
                current.Discard();
            }
        }

        internal void Drain(ref ExceptionAggregator exceptions)
        {
            for (Discardable? current = this, nextNode; current is not null; current = nextNode)
            {
                nextNode = current.next;
                current.next = null; // help GC

                try
                {
                    current.Discard();
                }
                catch (Exception e)
                {
                    exceptions += e;
                }
            }
        }

        internal void Drain(bool throwOnFirstException)
        {
            if (throwOnFirstException)
            {
                Drain();
            }
            else
            {
                var exceptions = new ExceptionAggregator();
                Drain(ref exceptions);
                exceptions.ThrowIfNeeded();
            }
        }

        void IThreadPoolWorkItem.Execute()
        {
            var exceptions = new ExceptionAggregator();
            Drain(ref exceptions);
            exceptions.ThrowIfNeeded();
        }

        internal void Attach(ref Discardable? location)
        {
            for (Discardable? current = location, tmp;; current = tmp)
            {
                next = current;
                tmp = Interlocked.CompareExchange(ref location, this, current);
                if (ReferenceEquals(tmp, current))
                    break;
            }
        }

        internal static Discardable? Detach(ref Discardable? location)
            => location is not null ? Interlocked.Exchange(ref location, null) : null;
    }
    
    private sealed class ActionNode(Action callback) : Discardable
    {
        protected override void Discard() => callback();
    }

    private sealed class ActionNode<T>(T arg, Action<T> callback) : Discardable
    {
        protected override void Discard() => callback(arg);
    }

    private sealed class WorkItem<TWorkItem>(TWorkItem item) : Discardable
        where TWorkItem : struct, IThreadPoolWorkItem
    {
        private TWorkItem item = item;
        protected override void Discard() => item.Execute();
    }

    private sealed class Cleanup(IDisposable disposable) : Discardable
    {
        protected override void Discard() => disposable.Dispose();
    }
}