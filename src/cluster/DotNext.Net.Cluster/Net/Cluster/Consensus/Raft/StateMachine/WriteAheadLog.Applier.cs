using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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
        for (long newIndex; !token.IsCancellationRequested && backgroundTaskFailure is null; await applyTrigger.WaitAsync(token).ConfigureAwait(false))
        {
            newIndex = LastCommittedEntryIndex;

            // Ensure that the appender is not running with the snapshot installation process concurrently
            await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
            try
            {
                await ApplyAsync(LastAppliedIndex + 1L, newIndex, token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException canceledEx || canceledEx.CancellationToken != token)
            {
                backgroundTaskFailure = ExceptionDispatchInfo.Capture(e);
                appliedEvent.Interrupt(e);
                break;
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        private set
        {
            appliedIndex = value;
            Interlocked.MemoryBarrier();
            appliedEvent.Signal(resumeAll: true);
        }
    }

    // returns snapshot index
    private async ValueTask ApplyAsync(long fromIndex, long toIndex, CancellationToken token)
    {
        var ts = new Timestamp();
        for (long index = fromIndex, appliedIndex; index <= toIndex; index = long.Max(appliedIndex + 1L, index + 1L))
        {
            if (metadataPages[index] is { HasPayload: true } metadata)
            {
                var entry = new LogEntry(metadata, index, dataPages)
                {
                    Context = context.Remove(index, out var ctx) ? ctx : null,
                };

                appliedIndex = await stateMachine.ApplyAsync(entry, token).ConfigureAwait(false);
            }
            else
            {
                appliedIndex = index;
            }

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