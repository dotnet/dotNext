using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Diagnostics;
using IO.Log;
using Runtime.CompilerServices;
using Threading;

partial class WriteAheadLog
{
    private readonly AsyncAutoResetEventSlim flushTrigger, flushCompleted;
    private readonly Task flusherTask;
    private readonly WeakReference<Task?> cleanupTask = new(target: null, trackResurrection: false);
    
    private Checkpoint checkpoint;
    private Atomic<FlushState> flusherState;
    private long flusherOldSnapshot;

    private async Task FlushAsync<TBackground>(CancellationToken token)
        where TBackground : struct, IConstant<bool>
    {
        if (TBackground.Value)
        {
            await Task.Yield();
        }

        try
        {
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

                StartCleanupIfNeeded(newSnapshot);

                flushCompleted.Set();
                if (!TBackground.Value || !await flushTrigger.WaitAsync().ConfigureAwait(false))
                    break;
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token && TBackground.Value)
        {
            // suspend
        }
        catch (Exception e) when (TBackground.Value)
        {
            backgroundTaskFailure = e;
            appliedEvent.Interrupt(new InternalException(e));
        }
        finally
        {
            // wake up the waiters to let them observe the failure
            flushCompleted.Set();
        }
    }

    private void StartCleanupIfNeeded(long snapshotIndex)
    {
        // Weak ref tracks the task, but allows GC to collect associated state machine
        // as soon as possible. While the task is running, it cannot be collected, because it's referenced
        // by the async state machine.
        if ((!cleanupTask.TryGetTarget(out var task) || task.IsCompletedSuccessfully) && flusherOldSnapshot < snapshotIndex)
        {
            cleanupTask.SetTarget(CleanUpAsync(snapshotIndex, lifetimeToken));
            flusherOldSnapshot = snapshotIndex;
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

    private async Task EnsureFlushedAsync(long endIndex, CancellationToken token)
    {
        // the flusher might be idle, e.g. after the snapshot installation
        flushTrigger.Set();

        var linkedTokenSource = cancellationTokens.Combine(token, lifetimeToken);
        var registration = linkedTokenSource.Token.UnsafeRegister(Signal, flushCompleted);
        try
        {
            while (flusherState.Read<long, UnflushedIndexField>() <= endIndex)
            {
                ThrowOnInternalError();
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
        => EnsureFlushedAsync(LastEntryIndex, token);

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
        flushTrigger.Set();
        CommitRateMeter.Add(count, measurementTags);
    }

    private long UnflushedIndex
    {
        set
        {
            using var scope = flusherState.EnterLock();
            scope.Value.UnflushedIndex = value;
        }
    }
    
    [StructLayout(LayoutKind.Auto)]
    private struct FlushState
    {
        public long UnflushedIndex, FlushedCommitIndex;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct UnflushedIndexField : IFieldReference<FlushState, long>
    {
        static ref readonly long IFieldReference<FlushState, long>.GetReference(in FlushState value)
            => ref value.UnflushedIndex;
    }
}