namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;

[Collection(TestCollections.WriteAheadLog)]
public sealed class RegressionIssue292 : Test
{
    [Theory]
    [InlineData(500)]
    [InlineData(512)]
    public async Task EntriesAcrossChunkBoundary(int payloadSize)
    {
        var machine = new RecordingStateMachine();
        var location = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        await using (var log = new WriteAheadLog(new WriteAheadLog.Options { Location = location }, machine))
        {
            await log.InitializeAsync(TestToken);

            var index = 0L;
            for (var marker = 1; marker <= 20; marker++)
            {
                index = await log.AppendAsync(new RawEntry(payloadSize, (byte)marker), TestToken);
                await log.CommitAsync(index, TestToken);
            }

            await log.WaitForApplyAsync(index, TestToken);
        }

        Assert.Equal(20, machine.Applied.Count);

        foreach (var (marker, length, first) in machine.Applied)
        {
            Assert.Equal(payloadSize, length);
            Assert.Equal(marker, first);
        }
    }
}

file readonly struct RawEntry(int size, byte marker) : IRaftLogEntry
{
    public long Term => 1L;
    public int? CommandId => marker;
    public bool IsSnapshot => false;
    public long? Length => size;
    public bool IsReusable => true;

    public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        where TWriter : IAsyncBinaryWriter
    {
        var payload = new byte[size];
        Array.Fill(payload, marker);
        return writer.WriteAsync(payload, null, token);
    }
}

file sealed class RecordingStateMachine : IStateMachine
{
    public List<(int Marker, long Length, byte First)> Applied { get; } = [];

    public ISnapshot Snapshot => null;

    public ValueTask ReclaimGarbageAsync(long watermark, CancellationToken token)
        => ValueTask.CompletedTask;

    public ValueTask<long> ApplyAsync(LogEntry entry, CancellationToken token)
    {
        if (entry is { IsConfiguration: false, IsSnapshot: false, CommandId: { } marker, Length: > 0 }
            && entry.TryGetPayload(out var payload))
        {
            Applied.Add((marker, payload.Length, payload.FirstSpan[0]));
        }

        return ValueTask.FromResult(entry.Index);
    }
}