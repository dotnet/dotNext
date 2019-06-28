using System;
using System.Threading;

namespace DotNext.Runtime.CompilerServices
{
    internal sealed class Continuation
    {
        private static readonly Func<object, Action> ContinuationWithoutContextFactory = DelegateHelpers.CreateClosedDelegateFactory<Action>(() => QueueContinuation(null));

        private readonly Action callback;
        private readonly SynchronizationContext context;

        private Continuation(Action callback, SynchronizationContext context)
        {
            this.callback = callback;
            this.context = context;
        }

        private static void Invoke(object continuation) => (continuation as Action)?.Invoke();

        private static void QueueContinuation(Action callback) => ThreadPool.QueueUserWorkItem(Invoke, callback);

        private void Invoke() => context.Post(Invoke, callback);

        internal static Action Create(Action callback)
        {
            var context = SynchronizationContext.Current?.CreateCopy();
            return context is null ? ContinuationWithoutContextFactory(callback) : new Continuation(callback, context).Invoke;
        }
    }
}