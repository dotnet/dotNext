using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.ReplicationUtils;

using Diagnostics;
using IO;
using IO.Log;
using Threading;

internal class ReplicationProcess : Disposable
{
    public required IPersistentState AuditTrail { get; init; }

    public virtual void Replicate(ReplicationBarrier barrier)
        => barrier.SetResult(MemberResult.Committed(AuditTrail.LastEntryIndex));

    public virtual void Start(CancellationToken token)
    {
        // nothing to do
    }

    public virtual Task StopAsync(bool interrupt = false) => Task.CompletedTask;

    public virtual bool IsAvailable => true;
}

internal sealed class ReplicationProcess<TMember> : ReplicationProcess, ILogEntryConsumer<IRaftLogEntry, Result<HeartbeatResult>>
    where TMember : IRaftClusterMember
{
    private readonly TMember member;
    private readonly ChannelReader<ReplicationBarrier> reader;
    private readonly ChannelWriter<ReplicationBarrier> writer;
    private readonly CancellationTokenSource interruption;
    private long replicationIndex, precedingTerm;
    private bool available = true;
    private IFailureDetector? detector;

    public ReplicationProcess(TMember member, int queueSize)
    {
        this.member = member;

        var channel = Channel.CreateBounded<ReplicationBarrier>(new BoundedChannelOptions(queueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = true,
        });

        reader = channel.Reader;
        writer = channel.Writer;
        interruption = new();
    }

    public override bool IsAvailable => Volatile.Read(ref available);
    
    public required long Term { get; init; }

    public required ILogger Logger { get; init; }
    
    public IFailureDetector? FailureDetector
    {
        init => detector = value;
    }

    public override void Replicate(ReplicationBarrier barrier)
    {
        // If member is too slow and cannot process the queue, we assume that it's temporary unavailable
        if (!writer.TryWrite(barrier))
        {
            barrier.SetResult(MemberResult.Unavailable);
            Logger.SlowMember(member.EndPoint);
        }
    }

    public override void Start(CancellationToken token) => _ = ReplicateAsync(token);

    public override Task StopAsync(bool interrupt = false)
    {
        if (interrupt)
            interruption.Cancel(throwOnFirstException: false);

        writer.Complete();
        return reader.Completion;
    }

    private async Task ReplicateAsync(CancellationToken token)
    {
        // There are two cancellation tokens:
        // First one represents leadership token maintained by the Leader state;
        // Second one is a token that can be used to abort any communication with the member. This is useful
        // when the member needs to be removed due to membership changes.
        using var source = CancellationToken.Combine(token, interruption.Token);
        
        // Do not pass the token to WaitToReadAsync(), because we want to read all the signals from the channel
        // even in case of cancellation
        while (await reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
        {
            for (MemberResult? result; reader.TryRead(out var barrier); barrier.SetResult(result))
            {
                replicationIndex = member.State.PrecedingIndex;
                try
                {
                    precedingTerm = await AuditTrail.GetTermAsync(replicationIndex, source.Token).ConfigureAwait(false);
                    var response = available
                        ? await AuditTrail
                            .ReadAsync(this, replicationIndex + 1L, AuditTrail.LastEntryIndex, source.Token)
                            .ConfigureAwait(false)
                        : throw new MemberUnavailableException(member);

                    detector?.ReportHeartbeat();
                    result = ConvertToResult(in response);
                }
                catch (MemberUnavailableException)
                {
                    result = MemberResult.Unavailable;
                }
                catch (OperationCanceledException e) when (e.CausedBy(source, token))
                {
                    result = MemberResult.Canceled;
                    detector = null; // disable failure detection
                    // continue loop to drain the channel
                }
                catch (OperationCanceledException e) when (e.CausedBy(source, interruption.Token))
                {
                    // the process has been interrupted, report this member as unavailable and disable failure detection
                    result = MemberResult.Unavailable;
                    detector = null;
                }
                catch (Exception e)
                {
                    Logger.LogError(e, ExceptionMessages.UnexpectedError);
                    result = MemberResult.Unavailable;
                }

                CheckHealthStatus();
            }
        }
    }

    private void CheckHealthStatus()
    {
        switch (detector)
        {
            case { IsMonitoring: false }:
                Logger.UnknownHealthStatus(member.EndPoint);
                break;
            case { IsHealthy: false }:
                Volatile.Write(ref available, false);
                detector = null; // disable failure detection
                break;
        }
    }

    private MemberResult? ConvertToResult(in Result<HeartbeatResult> result)
    {
        switch (result.Value)
        {
            case HeartbeatResult.ReplicatedWithLeaderTerm:
                OnReplicated();
                return MemberResult.Committed(replicationIndex);
            case HeartbeatResult.Replicated:
                OnReplicated();
                return MemberResult.Touched;
            case HeartbeatResult.Rejected when result.Term > Term:
                return MemberResult.HigherTermDetected(result.Term);
            default:
                Logger.ReplicationFailed(member.EndPoint, member.State.NextIndex = member.State.PrecedingIndex);
                return MemberResult.Touched;
        }
    }

    private void OnReplicated()
    {
        Logger.ReplicationSuccessful(member.EndPoint, member.State.NextIndex);
        member.State.NextIndex = replicationIndex + 1L;
    }
    
    ValueTask<Result<HeartbeatResult>> ILogEntryConsumer<IRaftLogEntry, Result<HeartbeatResult>>.
        ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
        => new(snapshotIndex.HasValue
            ? ReplicateSnapshotAsync(entries[0], snapshotIndex.GetValueOrDefault(), token)
            : ReplicateEntriesAsync<TEntryImpl, TList>(entries, token));

    private Task<Result<HeartbeatResult>> ReplicateEntriesAsync<TEntry, TList>(TList entries, CancellationToken token)
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>
    {
        Logger.ReplicaSize(member.EndPoint, entries.Count, replicationIndex, precedingTerm);
        var result = member.AppendEntriesAsync<TEntry, TList>(Term, entries, replicationIndex, precedingTerm,
            AuditTrail.LastCommittedEntryIndex, token);
        replicationIndex += entries.Count;
        return result;
    }

    private async Task<Result<HeartbeatResult>> ReplicateSnapshotAsync<TSnapshot>(TSnapshot snapshot,
        long snapshotIndex, CancellationToken token)
        where TSnapshot : IRaftLogEntry
    {
        Debug.Assert(snapshot.IsSnapshot);

        Logger.InstallingSnapshot(member.EndPoint, replicationIndex = snapshotIndex);

        var (config, version) = await LoadConfigurationAsync(token).ConfigureAwait(false);
        return await member.InstallSnapshotAsync(Term, snapshot, snapshotIndex, config, version, token)
            .ConfigureAwait(false);
    }

    private ValueTask<(IDataTransferObject, long)> LoadConfigurationAsync(CancellationToken token)
        => AuditTrail.ConfigurationStorage?.LoadConfigurationAsync(token) ??
           ValueTask.FromResult((IDataTransferObject.Empty, 0L));

    public async ValueTask<bool> CatchUpAsync(int rounds, CancellationToken token)
    {
        var watermarkIndex = AuditTrail.LastCommittedEntryIndex;
        Start(token);

        for (var barrier = new ReplicationBarrier(); rounds > 0; rounds--, barrier.Reuse())
        {
            var result = await ReplicateSingleAsync(barrier).ConfigureAwait(false);

            switch (barrier[0])
            {
                case { IsCanceled: true }:
                    throw new OperationCanceledException(token);
                case { Term: not null }:
                    rounds = 0;
                    break;
                case var memberResult when result.HasConsensus && memberResult.CommitIndex >= watermarkIndex:
                    writer.Complete();
                    return true;
            }
        }

        writer.Complete();
        return false;
    }

    private ValueTask<ReplicationResult> ReplicateSingleAsync(ReplicationBarrier barrier)
    {
        var task = barrier.WaitAsync(memberCount: 1);
        Replicate(barrier);
        return task;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            interruption.Dispose();
            writer.TryComplete(new ObjectDisposedException(GetType().Name));
        }
        
        base.Dispose(disposing);
    }
}