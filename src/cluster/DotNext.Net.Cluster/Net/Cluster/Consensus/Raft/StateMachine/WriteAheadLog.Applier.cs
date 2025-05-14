using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Diagnostics;
using Runtime.CompilerServices;
using Threading;

partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly AsyncAutoResetEvent applyTrigger;
    private readonly Task appenderTask;
    
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly AsyncTrigger appliedEvent;
    private long appliedIndex; // volatile, only applier can modify the field

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ApplyAsync(CancellationToken token)
    {
        for (long newIndex; !IsDisposingOrDisposed; await applyTrigger.WaitAsync(token).ConfigureAwait(false))
        {
            newIndex = LastCommittedEntryIndex;
            
            // Ensure that the appender is not running with the snapshot installation process concurrently
            await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
            try
            {
                await ApplyAsync(appliedIndex + 1L, newIndex, token).ConfigureAwait(false);
            }
            finally
            {
                lockManager.ReleaseReadLock();
            }

            LastAppliedIndex = newIndex;
        }
    }

    /// <summary>
    /// Gets the index of the last log entry applied to the underlying state machine.
    /// </summary>
    public long LastAppliedIndex
    {
        get => Volatile.Read(in appliedIndex);
        private set
        {
            Volatile.Write(ref appliedIndex, value);
            appliedEvent.Signal(resumeAll: true);
        }
    }

    // returns snapshot index
    private async ValueTask ApplyAsync(long fromIndex, long toIndex, CancellationToken token)
    {
        var ts = new Timestamp();
        for (long index = fromIndex, appliedIndex; index <= toIndex; index = long.Max(appliedIndex + 1L, index + 1L))
        {
            var entry = new LogEntry(metadataPages.Read(index, dataPages, out var metadata), in metadata, index)
            {
                Context = context.Remove(index, out var ctx) ? ctx : null,
            };

            appliedIndex = await stateMachine.ApplyAsync(entry, token).ConfigureAwait(false);
            Debug.Assert(appliedIndex <= toIndex);
            
            ApplyRateMeter.Add(1L, measurementTags);
        }

        ApplyDurationMeter.Record(ts.ElapsedMilliseconds);
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct CommitChecker : ISupplier<bool>
    {
        private readonly WriteAheadLog log;
        private readonly long index;

        internal CommitChecker(WriteAheadLog log, long index)
        {
            Debug.Assert(log is not null);

            this.log = log;
            this.index = index;
        }

        bool ISupplier<bool>.Invoke() => index <= log.LastAppliedIndex;
    }
}