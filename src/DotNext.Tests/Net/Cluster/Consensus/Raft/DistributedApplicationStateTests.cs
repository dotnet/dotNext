using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using DistributedServices;

    public sealed class DistributedApplicationStateTests : Assert
    {
        private const int RecordsPerPartition = 4;

        private static void GenerateIds(out ClusterMemberId id1, out ClusterMemberId id2)
        {
            var rnd = new Random();
            id1 = rnd.Next<ClusterMemberId>();
            id2 = rnd.Next<ClusterMemberId>();
            NotEqual(id1, id2);
        }

        [Fact]
        public static async Task LockManagement()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            await using var state = new DistributedApplicationState(dir, RecordsPerPartition);
            IDistributedLockEngine engine = state;
            GenerateIds(out var id1, out var id2);
            var version1 = Guid.NewGuid();
            var version2 = Guid.NewGuid();
            //acquire locks
            True(await engine.RegisterAsync("lock1", new DistributedLock 
            { 
                Owner = id1, 
                CreationTime = DateTimeOffset.Now, 
                Version = version1, 
                LeaseTime = TimeSpan.FromHours(1)  
            }, CancellationToken.None));
            True(await engine.RegisterAsync("lock2", new DistributedLock 
            { 
                Owner = id2, 
                CreationTime = DateTimeOffset.Now, 
                Version = version2, 
                LeaseTime = TimeSpan.FromHours(1)  
            }, CancellationToken.None));
            //attempts to acquire lock which was previously acquired
            False(await engine.RegisterAsync("lock1", new DistributedLock 
            { 
                Owner = id2, 
                CreationTime = DateTimeOffset.Now, 
                Version = Guid.NewGuid(), 
                LeaseTime = TimeSpan.FromHours(1)  
            }, CancellationToken.None));
            //check registration
            True(engine.IsRegistered("lock1", id1, version1));
            True(engine.IsRegistered("lock2", id2, version2));
            False(engine.IsRegistered("lock1", id1, version2));
            False(engine.IsRegistered("lock2", id2, version1));
            //remove locks
            False(await engine.UnregisterAsync("lock1", id1, version2, CancellationToken.None));
            True(await engine.UnregisterAsync("lock1", id1, version1, CancellationToken.None));
            False(engine.IsRegistered("lock1", id1, version1));
            True(engine.IsRegistered("lock2", id2, version2));
            await engine.UnregisterAsync("lock2", CancellationToken.None);
            False(engine.IsRegistered("lock2", id2, version2));
        }

        [Fact]
        public static async Task AcquireAndWait()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            await using var state = new DistributedApplicationState(dir, RecordsPerPartition);
            IDistributedLockEngine engine = state;
            GenerateIds(out var id1, out var id2);
            var version1 = Guid.NewGuid();
            var task = engine.WaitForLockEventAsync(true, TimeSpan.FromMinutes(10), CancellationToken.None);
            True(await engine.RegisterAsync("lock1", new DistributedLock
            {
                Owner = id1,
                Version = version1,
                LeaseTime = TimeSpan.FromHours(1),
                CreationTime = DateTimeOffset.Now
            }, CancellationToken.None));
            //acquire lock in parallel thread
            ThreadPool.QueueUserWorkItem<IPersistentState>(async state => 
            {
                await state.CommitAsync(CancellationToken.None);
            }, state, false);
            await task;
            True(engine.IsRegistered("lock1", id1, version1));
            False(engine.IsRegistered("lock1", id2, version1));
        }

        [Fact]
        public static async Task Compaction()
        {
            GenerateIds(out var id1, out var id2);
            GenerateIds(out var id3, out var id4);
            GenerateIds(out var id5, out var id6);
            var version1 = Guid.NewGuid();
            var version2 = Guid.NewGuid();
            var version3 = Guid.NewGuid();
            var version4 = Guid.NewGuid();
            var version5 = Guid.NewGuid();
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            IDistributedLockEngine engine;
            using(var state = new DistributedApplicationState(dir, RecordsPerPartition))
            {
                engine = state;
                //acquire 5 locks
                True(await engine.RegisterAsync("lock1", new DistributedLock
                {
                    Owner = id1,
                    Version = version1,
                    LeaseTime = TimeSpan.FromHours(1),
                    CreationTime = DateTimeOffset.Now
                }, CancellationToken.None));
                True(await engine.RegisterAsync("lock2", new DistributedLock
                {
                    Owner = id2,
                    Version = version2,
                    LeaseTime = TimeSpan.FromHours(1),
                    CreationTime = DateTimeOffset.Now
                }, CancellationToken.None));
                True(await engine.RegisterAsync("lock3", new DistributedLock
                {
                    Owner = id3,
                    Version = version3,
                    LeaseTime = TimeSpan.FromHours(1),
                    CreationTime = DateTimeOffset.Now
                }, CancellationToken.None));
                True(await engine.RegisterAsync("lock4", new DistributedLock
                {
                    Owner = id4,
                    Version = version4,
                    LeaseTime = TimeSpan.FromHours(1),
                    CreationTime = DateTimeOffset.Now
                }, CancellationToken.None));
                True(await engine.RegisterAsync("lock5", new DistributedLock
                {
                    Owner = id5,
                    Version = version5,
                    LeaseTime = TimeSpan.FromHours(1),
                    CreationTime = DateTimeOffset.Now
                }, CancellationToken.None));
                //commit all locks
                await state.CommitAsync(CancellationToken.None);
            }
            //reconstruct lock state
            using(var state = new DistributedApplicationState(dir, RecordsPerPartition))
            {
                engine = state;
                await state.ReplayAsync();
                True(engine.IsRegistered("lock1", id1, version1));
                True(engine.IsRegistered("lock2", id2, version2));
                True(engine.IsRegistered("lock3", id3, version3));
                True(engine.IsRegistered("lock4", id4, version4));
                True(engine.IsRegistered("lock5", id5, version5));
                False(engine.IsRegistered("lock1", id6, version1));
            }
        }
    }
}