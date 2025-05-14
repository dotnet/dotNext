using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

[Experimental("DOTNEXT001")]
public sealed class WriteAheadLogTests : Test
{
    [Fact]
    public static async Task LockManager()
    {
        await using var lockManager = new WriteAheadLog.LockManager(3);
        await lockManager.AcquireReadLockAsync();

        var readBarrierTask = lockManager.AcquireReadBarrierAsync().AsTask();
        lockManager.ReleaseReadLock();

        await readBarrierTask.WaitAsync(DefaultTimeout);
    }
}