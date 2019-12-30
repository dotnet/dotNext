using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RaftNode
{
    internal sealed class SimplePersistentState : PersistentState, IValueProvider
    {
        internal const string LogLocation = "logLocation";
        private const string ContentFile = "content.bin";

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

        private readonly FileStream content;

        private SimplePersistentState(string path)
            : base(path, 50, new Options { InitialPartitionSize = 50 * 8 })
        {
            content = new FileStream(Path.Combine(path, ContentFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1024, true);
            //pre-allocate file for better performance
            content.SetLength(sizeof(long));
        }

        public SimplePersistentState(IConfiguration configuration)
            : this(configuration[LogLocation])
        {
        }

        async Task<long> IValueProvider.GetValueAsync()
        {
            using (await WriteLock.AcquireAsync(CancellationToken.None).ConfigureAwait(false))
            {
                content.Position = 0;
                return await content.ReadAsync<long>(Buffer).ConfigureAwait(false);
            }
        }

        protected override async ValueTask ApplyAsync(LogEntry entry)
        {
            var value = await entry.ReadAsync<long>().ConfigureAwait(false);
            content.Position = 0;
            Console.WriteLine($"Accepting value {value}");
            await content.WriteAsync(value, Buffer).ConfigureAwait(false);
        }

        protected override ValueTask FlushAsync() => new ValueTask(content.FlushAsync());

        protected override SnapshotBuilder CreateSnapshotBuilder()
        {
            Console.WriteLine("Building snapshot");
            return new SimpleSnapshotBuilder();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                content.Dispose();
            base.Dispose(disposing);
        }
    }
}
