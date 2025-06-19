using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Diagnostics;
using IO.Log;
using Threading;

partial class WriteAheadLog
{
    private readonly AsyncAutoResetEventSlim? flushTrigger, flushCompleted;
    private readonly Task flusherTask;
    private readonly WeakReference<Task?> cleanupTask = new(target: null, trackResurrection: false);
    
    private Checkpoint checkpoint;
    private long commitIndex; // Commit lock protects modification of this field
    private long flusherPreviousIndex, flusherOldSnapshot;

    private async Task FlushAsync<T>(T flushTrigger, CancellationToken token)
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
                var newSnapshot = SnapshotIndex;
                var newIndex = LastCommittedEntryIndex;

                if (newIndex >= flusherPreviousIndex)
                {
                    // Ensure that the flusher is not running with the snapshot installation process concurrently
                    lockManager.SetCallerInformation("Flush Pages");
                    await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        var ts = new Timestamp();
                        await Flush(flusherPreviousIndex, newIndex, token).ConfigureAwait(false);

                        // everything up to toIndex is flushed, save the commit index
                        await checkpoint.UpdateAsync(newIndex, token).ConfigureAwait(false);
                        FlushDurationMeter.Record(ts.ElapsedMilliseconds);
                    }
                    finally
                    {
                        lockManager.ReleaseReadLock();
                    }
                }

                if ((!cleanupTask.TryGetTarget(out var task) || task.IsCompletedSuccessfully) && flusherOldSnapshot < newSnapshot)
                    cleanupTask.SetTarget(CleanUpAsync(newSnapshot, lifetimeToken));

                flushTrigger.NotifyCompleted();
                Volatile.Write(ref flusherPreviousIndex, long.Max(flusherOldSnapshot = newSnapshot, newIndex) + 1L);
                if (!await flushTrigger.WaitAsync(token).ConfigureAwait(false))
                    break;
            }
        }
        catch (OperationCanceledException e) when (T.IsBackground && e.CancellationToken == token)
        {
            // suspend
        }
        catch (Exception e) when (T.IsBackground)
        {
            backgroundTaskFailure = ExceptionDispatchInfo.Capture(e);
            appliedEvent.Interrupt(e);
        }
        finally
        {
            (flushTrigger as IDisposable)?.Dispose();
        }
    }

    private Task Flush(long fromIndex, long toIndex, CancellationToken token)
    {
        var metadataTask = metadataPages.FlushAsync(fromIndex, toIndex, token).AsTask();

        var toMetadata = metadataPages[toIndex];
        var fromMetadata = metadataPages[fromIndex];
        var dataTask = dataPages.FlushAsync(fromMetadata.Offset, toMetadata.End, token).AsTask();

        FlushRateMeter.Add(toIndex - fromIndex, measurementTags);
        return Task.WhenAll(metadataTask, dataTask);
    }

    private async Task EnsureFlushedAsync(CancellationToken token)
    {
        Debug.Assert(flushCompleted is not null);

        var linkedTokenSource = token.LinkTo(lifetimeToken);
        var registration = token.UnsafeRegister(Signal, flushCompleted);
        try
        {
            while (Volatile.Read(in flusherPreviousIndex) < LastCommittedEntryIndex)
            {
                await flushCompleted.WaitAsync().ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    ThrowWhenCanceled(linkedTokenSource, token);
            }
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
            linkedTokenSource?.Dispose();
        }

        static void Signal(object? state)
        {
            Debug.Assert(state is AsyncAutoResetEventSlim);

            Unsafe.As<AsyncAutoResetEventSlim>(state).Set();
        }
    }
    
    [DoesNotReturn]
    private void ThrowWhenCanceled(LinkedCancellationTokenSource? cts, CancellationToken token)
    {
        CancellationToken sourceToken;
        if (cts is null)
        {
            sourceToken = token;
        }
        else if (cts.CancellationOrigin == lifetimeToken)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
        else
        {
            sourceToken = cts.CancellationOrigin;
        }

        throw new OperationCanceledException(sourceToken);
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
            : EnsureFlushedAsync(token);

    /// <inheritdoc cref="IAuditTrail.LastCommittedEntryIndex"/>
    public long LastCommittedEntryIndex
    {
        get => Volatile.Read(in commitIndex);
        private set => Volatile.Write(ref commitIndex, value);
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
    
    private interface IFlushTrigger
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
    }

    [StructLayout(LayoutKind.Auto)]
    [SuppressMessage("Usage", "CA1001", Justification = "False positive")]
    private readonly struct TimeoutTrigger : IFlushTrigger, IDisposable
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
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Checkpoint : IDisposable
    {
        private const string FileName = "checkpoint";

        private readonly SafeFileHandle handle;
        private readonly byte[] buffer;

        internal Checkpoint(DirectoryInfo location, out long value)
        {
            var path = Path.Combine(location.FullName, FileName);

            // read the checkpoint
            using (var readHandle = File.OpenHandle(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                Span<byte> readBuf = stackalloc byte[sizeof(long)];
                value = RandomAccess.Read(readHandle, readBuf, 0L) >= sizeof(long)
                    ? BinaryPrimitives.ReadInt64LittleEndian(readBuf)
                    : 0L;
            }

            handle = File.OpenHandle(path, FileMode.Open, FileAccess.Write, options: FileOptions.Asynchronous | FileOptions.WriteThrough);
            buffer = GC.AllocateArray<byte>(sizeof(long), pinned: true);
        }

        public ValueTask UpdateAsync(long value, CancellationToken token)
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            return RandomAccess.WriteAsync(handle, buffer, fileOffset: 0L, token);
        }

        public void Dispose()
        {
            handle?.Dispose();
            this = default;
        }
    }
}