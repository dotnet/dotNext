using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Diagnostics;
using IO.Log;
using Threading;

partial class WriteAheadLog
{
    private readonly AsyncAutoResetEventSlim? flushTrigger, flushCompleted;
    private readonly Task flusherTask;
    private readonly WeakReference<Task?> cleanupTask = new(target: null, trackResurrection: false);
    private readonly bool flushOnCommit;
    
    private Checkpoint checkpoint;
    private Atomic<FlushState> flusherState;
    private long flusherOldSnapshot;

    private async Task FlushAsync<T>(T trigger, CancellationToken token)
        where T : struct, IFlushTrigger
    {
        if (T.IsBackground)
            await Task.Yield();
        
        // Weak ref tracks the task, but allows GC to collect associated state machine
        // as soon as possible. While the task is running, it cannot be collected, because it's referenced
        // by the async state machine.
        try
        {
            if (T.IsBackground)
            {
                flusherOldSnapshot = SnapshotIndex;
            }

            while (!token.IsCancellationRequested && backgroundTaskFailure is null)
            {
                var newSnapshot = SnapshotIndex; // everything is flushed and committed up to this index
                var lastCommittedIndex = long.Max(newSnapshot, LastCommittedEntryIndex);
                var currentState = flusherState.Value;
                var newIndex = long.Max(LastEntryIndex, newSnapshot);
                var flushFrom = long.Max(newSnapshot + 1L, currentState.UnflushedIndex);
                
                if (newIndex >= flushFrom || lastCommittedIndex != currentState.FlushedCommitIndex)
                {
                    // Ensure that the flusher is not running with the snapshot installation process concurrently
                    lockManager.SetCallerInformation("Flush Pages");
                    await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
                    
                    // Can be modified by AppendAsync which supports rewriting of the tail,
                    // that's why we need to read it again
                    currentState = flusherState.Value;
                    newIndex = long.Max(LastEntryIndex, newSnapshot);
                    flushFrom = long.Max(newSnapshot + 1L, currentState.UnflushedIndex);
                    var ts = default(Timestamp);
                    try
                    {
                        if (newIndex >= flushFrom)
                        {
                            ts = new();
                            await FlushAsync(flushFrom, newIndex, token).ConfigureAwait(false);

                            // everything up to newIndex is flushed, save the commit index
                            await checkpoint.UpdateAsync<CheckpointVersion1>(new(lastCommittedIndex, newIndex), token)
                                .ConfigureAwait(false);

                            currentState.UnflushedIndex = newIndex + 1L;
                        }
                        else if (lastCommittedIndex != currentState.FlushedCommitIndex)
                        {
                            ts = new();
                            
                            // update commit index only
                            await checkpoint.UpdateAsync<CheckpointVersion1>(new(lastCommittedIndex, newIndex), token)
                                .ConfigureAwait(false);

                            currentState.UnflushedIndex = flushFrom;
                        }
                        else
                        {
                            goto exit;
                        }

                        currentState.FlushedCommitIndex = lastCommittedIndex;
                        flusherState.Write(in currentState);
                        exit: ;
                    }
                    finally
                    {
                        if (!ts.IsEmpty)
                            FlushDurationMeter.Record(ts.ElapsedMilliseconds);
                        
                        lockManager.ReleaseReadLock();
                    }
                }

                if ((!cleanupTask.TryGetTarget(out var task) || task.IsCompletedSuccessfully) && flusherOldSnapshot < newSnapshot)
                    cleanupTask.SetTarget(CleanUpAsync(newSnapshot, lifetimeToken));

                trigger.NotifyCompleted();
                if (!await trigger.WaitAsync(token).ConfigureAwait(false))
                    break;
            }
        }
        catch (OperationCanceledException e) when (T.IsBackground && e.CancellationToken == token)
        {
            // suspend
        }
        catch (Exception e) when (T.IsBackground)
        {
            backgroundTaskFailure = e;
            appliedEvent.Interrupt(new InternalException(e));
        }
        finally
        {
            trigger.Dispose();
        }
    }

    private Task FlushAsync(long fromIndex, long toIndex, CancellationToken token)
    {
        var metadataTask = metadataPages.FlushAsync(fromIndex, toIndex, token).AsTask();

        var toMetadata = metadataPages.GetView<MetadataReader>(toIndex).Metadata;
        var fromMetadata = metadataPages.GetView<MetadataReader>(fromIndex).Metadata;
        var dataTask = dataPages.FlushAsync(fromMetadata.Offset, toMetadata.End, token).AsTask();

        FlushRateMeter.Add(toIndex - fromIndex + 1L, measurementTags);
        return Task.WhenAll(metadataTask, dataTask);
    }

    private async Task EnsureFlushedAsync<TChecker>(TChecker checker, CancellationToken token)
        where TChecker : struct, IFlushStateChecker
    {
        Debug.Assert(flushCompleted is not null);

        var linkedTokenSource = cancellationTokens.Combine(token, lifetimeToken);
        var registration = linkedTokenSource.Token.UnsafeRegister(Signal, flushCompleted);
        try
        {
            while (checker.IsNotFlushed(flusherState.Read<long, TChecker>()))
            {
                await flushCompleted.WaitAsync().ConfigureAwait(false);
                if (linkedTokenSource.Token.IsCancellationRequested)
                    ThrowWhenCanceled(linkedTokenSource);
            }
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
            await linkedTokenSource.DisposeAsync().ConfigureAwait(false);
        }

        static void Signal(object? state)
        {
            Debug.Assert(state is AsyncAutoResetEventSlim);

            Unsafe.As<AsyncAutoResetEventSlim>(state).Set();
        }
    }

    [DoesNotReturn]
    private void ThrowWhenCanceled(CancellationTokenMultiplexer.Scope cts)
    {
        ObjectDisposedException.ThrowIf(cts.CancellationOrigin == lifetimeToken, this);

        throw new OperationCanceledException(cts.CancellationOrigin);
    }

    /// <summary>
    /// Flushes and writes the checkpoint.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task FlushAsync(CancellationToken token = default)
        => flushCompleted is null
            ? FlushAsync<ForegroundTrigger>(new(), token)
            : flushOnCommit
                ? EnsureFlushedAsync(new FlushedIndexChecker(LastEntryIndex), token)
                : EnsureFlushedAsync(new FlushedCommitIndexChecker(LastCommittedEntryIndex), token);

    /// <inheritdoc cref="IAuditTrail.LastCommittedEntryIndex"/>
    public long LastCommittedEntryIndex
    {
        get => Atomic.Read(in field);
        private set => Atomic.Write(ref field, value);
    }
    
    private long Commit(long index)
    {
        var oldCommitIndex = LastCommittedEntryIndex;
        if (index > oldCommitIndex)
        {
            LastCommittedEntryIndex = index;
        }
        else
        {
            index = oldCommitIndex;
        }

        return index - oldCommitIndex;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void OnCommitted(long count)
    {
        applyTrigger.Set();
        flushTrigger?.Set();
        CommitRateMeter.Add(count, measurementTags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RequestFlushIfNeeded()
    {
        if (flushOnCommit)
        {
            Debug.Assert(flushTrigger is not null);
            
            flushTrigger.Set();
        }
    }

    private long UnflushedIndex
    {
        set
        {
            using var scope = flusherState.EnterLock();
            scope.Value.UnflushedIndex = value;
        }
    }
    
    private interface IFlushTrigger : IDisposable
    {
        ValueTask<bool> WaitAsync(CancellationToken token);

        void NotifyCompleted();

        static virtual bool IsBackground => true;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct BackgroundTrigger : IFlushTrigger
    {
        private readonly AsyncAutoResetEventSlim flushTrigger, flushNotification;
        
        public BackgroundTrigger(AsyncAutoResetEventSlim resetEvent, out AsyncAutoResetEventSlim notification)
        {
            flushTrigger = resetEvent;
            notification = flushNotification = new();
        }
        
        ValueTask<bool> IFlushTrigger.WaitAsync(CancellationToken token)
            => flushTrigger.WaitAsync();

        void IFlushTrigger.NotifyCompleted() => flushNotification.Set();

        void IDisposable.Dispose()
        {
            // nothing to do
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [SuppressMessage("Usage", "CA1001", Justification = "False positive")]
    private readonly struct TimeoutTrigger : IFlushTrigger
    {
        private readonly PeriodicTimer timer;
        private readonly AsyncAutoResetEventSlim flushNotification;

        public TimeoutTrigger(TimeSpan timeout, out AsyncAutoResetEventSlim notification)
        {
            timer = new(timeout);
            notification = flushNotification = new();
        }
        
        ValueTask<bool> IFlushTrigger.WaitAsync(CancellationToken token)
            => timer.WaitForNextTickAsync(token);

        void IFlushTrigger.NotifyCompleted() => flushNotification.Set();

        void IDisposable.Dispose() => timer.Dispose();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ForegroundTrigger : IFlushTrigger
    {
        ValueTask<bool> IFlushTrigger.WaitAsync(CancellationToken token)
            => ValueTask.FromResult(false);

        void IFlushTrigger.NotifyCompleted()
        {
        }

        static bool IFlushTrigger.IsBackground => false;
        
        void IDisposable.Dispose()
        {
            // nothing to do
        }
    }
    
    [StructLayout(LayoutKind.Auto)]
    private struct FlushState
    {
        public long UnflushedIndex, FlushedCommitIndex;
    }
    
    private interface IFlushStateChecker : Atomic<FlushState>.IFieldReference<long>
    {
        bool IsNotFlushed(long index);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct FlushedIndexChecker(long index) : IFlushStateChecker
    {
        bool IFlushStateChecker.IsNotFlushed(long unflushedIndex)
            => unflushedIndex < index;

        static ref readonly long Atomic<FlushState>.IFieldReference<long>.GetFieldReference(in FlushState value)
            => ref value.UnflushedIndex;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct FlushedCommitIndexChecker(long commitIndex) : IFlushStateChecker
    {
        bool IFlushStateChecker.IsNotFlushed(long flushedCommitIndex)
            => flushedCommitIndex < commitIndex;

        static ref readonly long Atomic<FlushState>.IFieldReference<long>.GetFieldReference(in FlushState value)
            => ref value.FlushedCommitIndex;
    }
}