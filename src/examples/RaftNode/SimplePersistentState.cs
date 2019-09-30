using DotNext.Net.Cluster.Consensus.Raft;
using Microsoft.Extensions.Configuration;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static DotNext.IO.StreamExtensions;

namespace RaftNode
{
    internal sealed class SimplePersistentState : PersistentState, IValueProvider
    {
        internal const string LogLocation = "logLocation";
        private const string ContentFile = "content.bin";

        private sealed class SimpleSnapshotBuilder : SnapshotBuilder
        {
            private readonly byte[] value;

            internal SimpleSnapshotBuilder() => value = new byte[sizeof(long)];

            public override Task CopyToAsync(Stream output, CancellationToken token) => output.WriteAsync(value, 0, value.Length);

            public override async ValueTask CopyToAsync(PipeWriter output, CancellationToken token) => await output.WriteAsync(value, token).ConfigureAwait(false);

            protected override async ValueTask ApplyAsync(LogEntry entry) => (await entry.ReadAsync(sizeof(long)).ConfigureAwait(false)).CopyTo(value);
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
            using (await SyncRoot.Acquire(CancellationToken.None).ConfigureAwait(false))
            {
                content.Position = 0;
                return await content.ReadAsync<long>(Buffer).ConfigureAwait(false);
            }
        }

        protected override async ValueTask ApplyAsync(LogEntry entry)
        {
            var value = await entry.ReadAsync(sizeof(long)).ConfigureAwait(false);
            content.Position = 0;
            Console.WriteLine($"Accepting value {BinaryPrimitives.ReadInt64LittleEndian(value.Span)}");
            await content.WriteAsync(value).ConfigureAwait(false);
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
