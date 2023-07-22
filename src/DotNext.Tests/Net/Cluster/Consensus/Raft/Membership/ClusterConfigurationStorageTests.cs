namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;
using HttpEndPoint = Net.Http.HttpEndPoint;

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
        var events = new Queue<(HttpEndPoint, bool)>();
        storage.ActiveConfigurationChanged += (address, added, token) =>
        {
            events.Enqueue((address, added));
            return ValueTask.CompletedTask;
        };

        Null(storage.ProposedConfiguration);
        Null(storage.As<IClusterConfigurationStorage>().ProposedConfiguration);
        Empty(storage.ActiveConfiguration);

        var ep = new HttpEndPoint(new Uri("https://localhost:3262", UriKind.Absolute));
        True(await storage.AddMemberAsync(ep));
        NotNull(storage.ProposedConfiguration);
        NotNull(storage.As<IClusterConfigurationStorage>().ProposedConfiguration);
        Empty(storage.ActiveConfiguration);
        Contains(ep, storage.ProposedConfiguration);
        var task = storage.WaitForApplyAsync();

        False(await storage.RemoveMemberAsync(ep));
        False(task.IsCompleted);

        await storage.ApplyAsync();
        True(task.IsCompletedSuccessfully);

        Null(storage.ProposedConfiguration);
        Null(storage.As<IClusterConfigurationStorage>().ProposedConfiguration);
        NotEmpty(storage.ActiveConfiguration);

        var ev = events.Dequeue();
        True(ev.Item2);
        Equal(ev.Item1, ep);

        Contains(ep, storage.ActiveConfiguration);

        True(await storage.RemoveMemberAsync(ep));
        task = storage.WaitForApplyAsync();
        False(task.IsCompleted);
        Empty(storage.ProposedConfiguration);

        await storage.ApplyAsync();
        Empty(storage.ActiveConfiguration);
        Null(storage.ProposedConfiguration);

        ev = events.Dequeue();
        False(ev.Item2);
        Equal(ev.Item1, ep);
    }

    [Fact]
    public static async Task InMemoryStorage()
    {
        using var storage = new InMemoryClusterConfigurationStorage();
        await storage.As<IClusterConfigurationStorage<HttpEndPoint>>().LoadConfigurationAsync();
        await StorageTest(storage);
    }

    [Fact]
    public static async Task PersistentStorage()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var storage = new PersistentClusterConfigurationStorage(path);
        await storage.As<IClusterConfigurationStorage<HttpEndPoint>>().LoadConfigurationAsync();
        await StorageTest(storage);
    }

    [Fact]
    public static async Task ConfigurationRecovery()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var ep = new HttpEndPoint(new Uri("https://localhost:3262", UriKind.Absolute));

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            await storage.LoadConfigurationAsync();
            True(await storage.AddMemberAsync(ep));
            await storage.ApplyAsync();
        }

        var ep2 = new HttpEndPoint(new Uri("https://localhost:3263", UriKind.Absolute));

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            await storage.LoadConfigurationAsync();
            True(await storage.AddMemberAsync(ep2));
            await storage.ApplyAsync();
        }

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            await storage.LoadConfigurationAsync();
            Null(storage.ProposedConfiguration);
            Contains(ep, storage.ActiveConfiguration);
            Contains(ep2, storage.ActiveConfiguration);
        }
    }

    [Fact]
    public static async Task ConfigurationExchange()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var ep = new HttpEndPoint(new Uri("https://localhost:3262", UriKind.Absolute));
        long fingerprint;
        byte[] configuration;

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            await storage.LoadConfigurationAsync();
            True(await storage.AddMemberAsync(ep));
            await storage.ApplyAsync();
            fingerprint = storage.As<IClusterConfigurationStorage>().ActiveConfiguration.Fingerprint;
            configuration = await storage.As<IClusterConfigurationStorage>().ActiveConfiguration.ToByteArrayAsync();
        }

        // create fresh storage and propose configuration
        path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            await storage.ProposeAsync(new SimpleConfigurationStorage(configuration, fingerprint));
            NotNull(storage.As<IClusterConfigurationStorage>().ProposedConfiguration);
            Contains(ep, storage.ProposedConfiguration);
        }

        // re-read proposed configuration
        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            await storage.LoadConfigurationAsync();
            NotNull(storage.As<IClusterConfigurationStorage>().ProposedConfiguration);
            Contains(ep, storage.ProposedConfiguration);
        }
    }

    [Fact]
    public static async Task CopyConfigurationAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var ep = new HttpEndPoint(new Uri("https://localhost:3262", UriKind.Absolute));

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            True(await storage.AddMemberAsync(ep));
            await storage.ApplyAsync();
        }

        using (IClusterConfigurationStorage storage = new PersistentClusterConfigurationStorage(path))
        {
            await storage.LoadConfigurationAsync();

            using var ms = new MemoryStream((int)storage.ActiveConfiguration.Length);
            await storage.ActiveConfiguration.WriteToAsync(ms);
            Equal(ms.Length, storage.ActiveConfiguration.Length);
        }
    }
}