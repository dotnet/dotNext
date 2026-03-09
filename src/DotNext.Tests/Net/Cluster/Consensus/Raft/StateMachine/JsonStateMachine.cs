using DotNext.IO;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Text.Json;

internal sealed class JsonStateMachine(DirectoryInfo location) 
    : SimpleStateMachine(new(Path.Combine(location.FullName, "db")))
{
    private readonly List<TestJsonObject> entries = new();

    protected override ValueTask RestoreAsync(FileInfo snapshotFile, CancellationToken token)
        => ValueTask.CompletedTask;

    protected override ValueTask PersistAsync(IAsyncBinaryWriter writer, CancellationToken token)
        => ValueTask.CompletedTask;

    protected override async ValueTask<bool> ApplyAsync(LogEntry entry, CancellationToken token)
    {
        var content = await JsonSerializable<TestJsonObject>.TransformAsync(entry, token);
        entries.Add(content);
        return false;
    }
    
    internal IReadOnlyList<TestJsonObject> Entries => entries;
}