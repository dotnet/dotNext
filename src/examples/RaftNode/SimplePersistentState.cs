using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using static DotNext.Threading.AtomicInt64;

namespace RaftNode
{

    internal sealed class SimplePersistentState : PersistentState, IValueProvider
    {
        internal const string LogLocation = "logLocation";

        private sealed class SimpleSnapshotBuilder : SnapshotBuilder
        {
            private long value;

            protected override async ValueTask ApplyAsync(LogEntry entry)
            {
                value = await entry.ReadAsync<long>().ConfigureAwait(false);
            }

            public override ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => writer.WriteAsync(value, token);
        }

        private long content;

        private SimplePersistentState(string path)
            : base(path, 50, new Options { InitialPartitionSize = 50 * 8 })
        {
        }

        public SimplePersistentState(IConfiguration configuration)
            : this(configuration[LogLocation])
        {
        }

        long IValueProvider.Value => content.VolatileRead();

        protected override async ValueTask ApplyAsync(LogEntry entry)
            => content.VolatileWrite(await entry.ReadAsync<long>().ConfigureAwait(false));

        protected override SnapshotBuilder CreateSnapshotBuilder()
        {
            Console.WriteLine("Building snapshot");
            return new SimpleSnapshotBuilder();
        }
    }
}
