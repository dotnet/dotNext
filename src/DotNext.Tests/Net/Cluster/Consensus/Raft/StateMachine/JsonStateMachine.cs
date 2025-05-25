using System.Diagnostics.CodeAnalysis;
using DotNext.IO;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Text.Json;

[Experimental("DOTNEXT001")]
internal sealed class JsonStateMachine : SimpleStateMachine
{
    private readonly List<TestJsonObject> entries = new();
    
    public JsonStateMachine(DirectoryInfo location)
        : base(new(Path.Combine(location.FullName, "db")))
    {
    }

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