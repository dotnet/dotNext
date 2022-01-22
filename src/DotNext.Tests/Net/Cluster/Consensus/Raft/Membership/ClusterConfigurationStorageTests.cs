using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    using Buffers;
    using IO;
    using static Collections.Generic.Sequence;
    using HttpEndPoint = Net.Http.HttpEndPoint;

    [ExcludeFromCodeCoverage]
    public sealed class ClusterConfigurationStorageTests : Test
    {
        private sealed class InMemoryClusterConfigurationStorage : InMemoryClusterConfigurationStorage<HttpEndPoint>
        {
            internal InMemoryClusterConfigurationStorage()
                : base(null)
            {
            }

            protected override HttpEndPoint Decode(ref SequenceReader reader)
                => (HttpEndPoint)reader.ReadEndPoint();

            protected override void Encode(HttpEndPoint address, ref BufferWriterSlim<byte> output)
                => output.WriteEndPoint(address);
        }

        private sealed class PersistentClusterConfigurationStorage : PersistentClusterConfigurationStorage<HttpEndPoint>
        {
            internal PersistentClusterConfigurationStorage(string path)
                : base(path)
            {
            }

            protected override HttpEndPoint Decode(ref SequenceReader reader)
                => (HttpEndPoint)reader.ReadEndPoint();

            protected override void Encode(HttpEndPoint address, ref BufferWriterSlim<byte> output)
                => output.WriteEndPoint(address);
        }

        private sealed class SimpleConfigurationStorage : BinaryTransferObject, IClusterConfiguration
        {
            internal SimpleConfigurationStorage(ReadOnlyMemory<byte> config, long fingerprint)
                : base(config)
                => Fingerprint = fingerprint;

            public long Fingerprint { get; }

            long IClusterConfiguration.Length => Content.Length;
        }

        private static async ValueTask StorageTest(IClusterConfigurationStorage<HttpEndPoint> storage)
        {
            Null(storage.ProposedConfiguration);
            Null(storage.As<IClusterConfigurationStorage>().ProposedConfiguration);
            Empty(storage.ActiveConfiguration);

            var ep = new HttpEndPoint(new Uri("https://localhost:3262", UriKind.Absolute));
            var id = ClusterMemberId.FromEndPoint(ep);
            True(await storage.AddMemberAsync(id, ep));
            NotNull(storage.ProposedConfiguration);
            NotNull(storage.As<IClusterConfigurationStorage>().ProposedConfiguration);
            Empty(storage.ActiveConfiguration);
            Contains(id, storage.ProposedConfiguration.Keys);
            Equal(ep, storage.ProposedConfiguration[id]);
            var task = storage.WaitForApplyAsync();

            False(await storage.RemoveMemberAsync(id));
            False(task.IsCompleted);

            await storage.ApplyAsync();
            True(task.IsCompletedSuccessfully);

            Null(storage.ProposedConfiguration);
            Null(storage.As<IClusterConfigurationStorage>().ProposedConfiguration);
            NotEmpty(storage.ActiveConfiguration);

            var ev = await storage.PollChangesAsync().FirstOrNoneAsync();
            True(ev.HasValue);
            True(ev.OrDefault().IsAdded);
            Equal(ev.OrDefault().Id, id);
            Equal(ev.OrDefault().Address, ep);

            Contains(id, storage.ActiveConfiguration.Keys);
            Equal(ep, storage.ActiveConfiguration[id]);

            True(await storage.RemoveMemberAsync(id));
            task = storage.WaitForApplyAsync();
            False(task.IsCompleted);
            Empty(storage.ProposedConfiguration);

            await storage.ApplyAsync();
            Empty(storage.ActiveConfiguration);
            Null(storage.ProposedConfiguration);

            ev = await storage.PollChangesAsync().FirstOrNoneAsync();
            True(ev.HasValue);
            False(ev.OrDefault().IsAdded);
            Equal(ev.OrDefault().Id, id);
            Equal(ev.OrDefault().Address, ep);
        }

        [Fact]
        public static async Task InMemoryStorage()
        {
            using var storage = new InMemoryClusterConfigurationStorage();
            await storage.LoadConfigurationAsync();
            await StorageTest(storage);
        }

        [Fact]
        public static async Task PersistentStorage()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            await using var storage = new PersistentClusterConfigurationStorage(path);
            await storage.LoadConfigurationAsync();
            await StorageTest(storage);
        }

        [Fact]
        public static async Task ConfigurationRecovery()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var ep = new HttpEndPoint(new Uri("https://localhost:3262", UriKind.Absolute));
            var id = ClusterMemberId.FromEndPoint(ep);

            await using (var storage = new PersistentClusterConfigurationStorage(path))
            {
                await storage.LoadConfigurationAsync();
                True(await storage.AddMemberAsync(id, ep));
                await storage.ApplyAsync();
            }

            await using (var storage = new PersistentClusterConfigurationStorage(path))
            {
                await storage.LoadConfigurationAsync();
                Null(storage.As<IClusterConfigurationStorage<HttpEndPoint>>().ProposedConfiguration);
                Equal(ep, storage.As<IClusterConfigurationStorage<HttpEndPoint>>().ActiveConfiguration[id]);
            }
        }

        [Fact]
        public static async Task ConfigurationExchange()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var ep = new HttpEndPoint(new Uri("https://localhost:3262", UriKind.Absolute));
            var id = ClusterMemberId.FromEndPoint(ep);
            long fingerprint;
            byte[] configuration;

            await using (var storage = new PersistentClusterConfigurationStorage(path))
            {
                await storage.LoadConfigurationAsync();
                True(await storage.AddMemberAsync(id, ep));
                await storage.ApplyAsync();
                fingerprint = storage.ActiveConfiguration.Fingerprint;
                configuration = await storage.ActiveConfiguration.ToByteArrayAsync();
            }

            // create fresh storage and propose configuration
            path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            await using (var storage = new PersistentClusterConfigurationStorage(path))
            {
                await storage.ProposeAsync(new SimpleConfigurationStorage(configuration, fingerprint));
                NotNull(storage.ProposedConfiguration);
                Equal(ep, storage.As<IClusterConfigurationStorage<HttpEndPoint>>().ProposedConfiguration[id]);
            }

            // re-read proposed configuration
            await using (var storage = new PersistentClusterConfigurationStorage(path))
            {
                await storage.LoadConfigurationAsync();
                NotNull(storage.ProposedConfiguration);
                Equal(ep, storage.As<IClusterConfigurationStorage<HttpEndPoint>>().ProposedConfiguration[id]);
            }
        }
    }
}