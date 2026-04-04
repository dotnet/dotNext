using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;
using HttpEndPoint = Net.Http.HttpEndPoint;

public sealed class ClusterConfigurationStorageTests : Test
{
    private sealed class InMemoryClusterConfigurationStorage : InMemoryClusterConfigurationStorage<HttpEndPoint>
    {
        protected override HttpEndPoint Decode(ref SequenceReader reader)
            => (HttpEndPoint)reader.ReadEndPoint();

        protected override void Encode(HttpEndPoint address, ref BufferWriterSlim<byte> writer)
            => writer.WriteEndPoint(address);
    }

    private sealed class PersistentClusterConfigurationStorage : PersistentClusterConfigurationStorage<HttpEndPoint>
    {
        internal PersistentClusterConfigurationStorage(string fileName)
            : base(fileName)
        {
        }

        protected override HttpEndPoint Decode(ref SequenceReader reader)
            => (HttpEndPoint)reader.ReadEndPoint();

        protected override void Encode(HttpEndPoint address, ref BufferWriterSlim<byte> writer)
            => writer.WriteEndPoint(address);
    }

    private static async ValueTask StorageTest(IClusterConfigurationStorage<HttpEndPoint> storage)
    {
        var (config, version) = await storage.As<IClusterConfigurationStorage>().LoadConfigurationAsync(TestToken);
        Equal(4L, config.Length);
        Equal(0L, version);
        
        var configuration = await storage.LoadConfigurationAsync(TestToken);
        Empty(configuration.Members);
        
        // rewrite version 0
        var address = new HttpEndPoint(IPAddress.Loopback, 4292, false);
        configuration = configuration.Add(address);
        await storage.SaveConfigurationAsync(configuration, configurationVersion: 0L, TestToken);
        configuration = await storage.LoadConfigurationAsync(TestToken);
        Equal(address, Single(configuration.Members));
        
        (config, version) = await storage.As<IClusterConfigurationStorage>().LoadConfigurationAsync(TestToken);
        Equal(await configuration.ToByteArrayAsync(token: TestToken), await config.ToByteArrayAsync(token: TestToken));
        Equal(0L, version);
        
        // try to rewrite version 0 again
        var address2 = new HttpEndPoint(IPAddress.Loopback, 4496, false);
        configuration = configuration.Add(address2);
        await storage.SaveConfigurationAsync(configuration, configurationVersion: 0L, TestToken);
        configuration = await storage.LoadConfigurationAsync(TestToken);
        DoesNotContain(address2, configuration.Members);
        Contains(address, configuration.Members);
        
        // install new version
        await storage.SaveConfigurationAsync(configuration, configurationVersion: 1L, TestToken);
        (config, version) = await storage.As<IClusterConfigurationStorage>().LoadConfigurationAsync(TestToken);
        Equal(await configuration.ToByteArrayAsync(token: TestToken), await config.ToByteArrayAsync(token: TestToken));
        Equal(1L, version);
    }

    [Fact]
    public static async Task InMemoryStorage()
    {
        using var storage = new InMemoryClusterConfigurationStorage();
        await StorageTest(storage);
    }

    [Fact]
    public static async Task PersistentStorage()
    {
        var path = GetTempPath();
        using var storage = new PersistentClusterConfigurationStorage(path);
        await StorageTest(storage);
    }

    [Fact]
    public static async Task ConfigurationRecovery()
    {
        var path = GetTempPath();
        var ep = new HttpEndPoint(new Uri("https://localhost:3262", UriKind.Absolute));

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            var configuration = await storage.LoadConfigurationAsync(TestToken);
            configuration = configuration.Add(ep);
            await storage.SaveConfigurationAsync(configuration, configurationVersion: 1L, TestToken);
        }

        var ep2 = new HttpEndPoint(new Uri("https://localhost:3263", UriKind.Absolute));

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            var configuration = await storage.LoadConfigurationAsync(TestToken);
            configuration = configuration.Add(ep2);
            await storage.SaveConfigurationAsync(configuration, configurationVersion: 2L, TestToken);
        }

        using (IClusterConfigurationStorage<HttpEndPoint> storage = new PersistentClusterConfigurationStorage(path))
        {
            var configuration = await storage.LoadConfigurationAsync(TestToken);
            Contains(ep, configuration.Members);
            Contains(ep2, configuration.Members);
        }
    }
}