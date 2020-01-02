using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading
{
    using Intrinsics = Runtime.Intrinsics;

    internal interface IEventHandler
    {
        void Receive();
    }

    /// <summary>
    /// Represents listener of asynchronous events.
    /// </summary>
    /// <remarks>
    /// The caller can be suspended using <see cref="SuspendAsync"/> method and
    /// resumed by <see cref="AsyncEventSource.Resume"/> method.
    /// The time between two suspensions can be enough to raise one or more events.
    /// In this case, the next call of <see cref="SuspendAsync"/> will be completed
    /// synchronously. However, multiple calls of <see cref="AsyncEventSource.Resume"/> will resume
    /// the suspended caller once.
    /// </remarks>
    public sealed class AsyncEventListener : Disposable, IAsyncDisposable, IValueTaskSource, IEventHandler
    {
        private readonly AsyncEventSource source;
        private bool state;
        private short version;
        private readonly CancellationTokenRegistration registration;
        [SuppressMessage("Usage", "CA2213", Justification = "The listener doesn't create or control lifetime of execution context")]
        private ExecutionContext? executionContext;
        private object? continuationState, syncContext;
        private Action<object?>? continuation;

        /// <summary>
        /// Attaches listener to the specific event source.
        /// </summary>
        /// <param name="source">The source of events.</param>
        /// <param name="token">The token that can be used to cancel listening.</param>
        public AsyncEventListener(AsyncEventSource source, CancellationToken token = default)
        {
            version = short.MinValue;
            registration = token.Register(Cancel);
            this.source = source;
            source.Attach(this);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Cancel()
        {
            if(continuation != null)
                RunContinuation(continuation, continuationState, syncContext, executionContext);
            continuation = null;
            continuationState = null;
            syncContext = null;
            executionContext = null;
        }

        /// <summary>
        /// Suspends the caller untile the event raised.
        /// </summary>
        /// <returns>The task representing event.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask SuspendAsync()
        {
            if(IsAborted)
                throw new OperationCanceledException(registration.Token);
            if(state)
            {
                version += 1;
                state = false;
                return new ValueTask();
            }
            return new ValueTask(this, version);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        void IValueTaskSource.GetResult(short token)
        {
            if(token < version)
                return;
            if(token > version)
                throw new InvalidOperationException();
            if(IsAborted)
                throw new OperationCanceledException(registration.Token);
            version += 1;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            if(token < version)
                return ValueTaskSourceStatus.Succeeded;
            if(token > version)
                return ValueTaskSourceStatus.Pending;
            if(IsAborted)
                return ValueTaskSourceStatus.Canceled;
            return state ? ValueTaskSourceStatus.Succeeded : ValueTaskSourceStatus.Pending;
        }

        private static void RunContinuation(Action<object?> continuation, object? state, object? syncContext)
        {
            switch(syncContext)
            {
                case null:
                        ThreadPool.QueueUserWorkItem(continuation, state, true);
                    break;
                case SynchronizationContext context:
                        context.Post(Unsafe.As<SendOrPostCallback>(continuation), state);
                    break;
                case TaskScheduler scheduler:
                        Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
                    break;
            }
        }

        private static void RunContinuation(Action<object?> continuation, object? state, object? syncContext, ExecutionContext? executionContext)
        {
            if(executionContext is null)
                RunContinuation(continuation, state, syncContext);
            else
                ExecutionContext.Run(executionContext, s => RunContinuation(continuation, s, syncContext), state);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            var executionContext = Intrinsics.HasFlag(flags, ValueTaskSourceOnCompletedFlags.FlowExecutionContext) ?
                ExecutionContext.Capture() :
                null;
            object? syncContext;
            if(Intrinsics.HasFlag(flags, ValueTaskSourceOnCompletedFlags.UseSchedulingContext))
            {
                syncContext = SynchronizationContext.Current;
                if(syncContext is null)
                {
                    var scheduler = TaskScheduler.Current;
                    syncContext = ReferenceEquals(scheduler, TaskScheduler.Default) ? null : scheduler;
                }
            }
            else
                syncContext = null;
            if(token < version)
                RunContinuation(continuation, state, syncContext, executionContext);
            else if(this.state)
            {
                this.state = false;
                RunContinuation(continuation, state, syncContext, executionContext);
            }
            else
            {
                this.syncContext = syncContext;
                this.executionContext = executionContext;
                this.continuation = continuation;
                this.continuationState = state;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]            
        void IEventHandler.Receive()
        {
            if(continuation is null)
                state = true;
            else
            {
                RunContinuation(continuation, continuationState, syncContext, executionContext);
                syncContext = null;
                executionContext = null;
                continuation = null;
                continuationState = null;
                state = false;
            }
        }

        /// <summary>
        /// Gets a value indicating that this listener is aborted
        /// and no longer receive events.
        /// </summary>
        public bool IsAborted => registration.Token.IsCancellationRequested;

        /// <summary>
        /// Releases all resources associated with this object
        /// and detaches listener from the source.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release managed and unmanaged resources; <see langword="false"/> to release unmanaged resources only.</param>
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                source.Detach(this);
                registration.Dispose();
                continuation = null;
                continuationState = null;
                executionContext = null;
                syncContext = null;
            }
            base.Dispose(disposing);
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            source.Detach(this);
            continuation = null;
            continuationState = null;
            executionContext = null;
            syncContext = null;
            return registration.DisposeAsync();
        }
    }

    /// <summary>
    /// Represents source of asynchronous events.
    /// </summary>
    public sealed class AsyncEventSource
    {
        private volatile Action? receivers;

        /// <summary>
        /// Resumes all suspended event listeners
        /// synchronously.
        /// </summary>
        public void Resume()
        {
            foreach(Action action in receivers?.GetInvocationList() ?? Array.Empty<Action>())
                action.Invoke();
        }

        /// <summary>
        /// Resumes all attached event listeners
        /// asynchronously.
        /// </summary>
        /// <param name="scheduler">The sheduler used to execute event listeners; or <see langword="null"/> to use <see cref="TaskScheduler.Current"/>.</param>
        /// <param name="token">The token that can be used to cancel execution.</param>
        /// <returns>The task that can be used to synchronize with all invoked listeners.</returns>
        public Task ResumeAsync(TaskScheduler? scheduler = null, CancellationToken token = default)
        {
            ICollection<Task> tasks = new LinkedList<Task>();
            foreach(Action action in receivers?.GetInvocationList() ?? Array.Empty<Action>())
                tasks.Add(Task.Factory.StartNew(action, token, TaskCreationOptions.None, scheduler ?? TaskScheduler.Current));
            return Task.WhenAll(tasks);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Attach(IEventHandler receiver)
            => receivers += receiver.Receive;
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Detach(IEventHandler receiver)
            => receivers -= receiver.Receive;
        
        /// <summary>
        /// Removes all event listeners attached to this source.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear() => receivers = null;
    }
}