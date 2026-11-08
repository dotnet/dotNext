using System.Buffers.Binary;
using System.Threading.Channels;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;

[Collection(TestCollections.WriteAheadLog)]
public sealed class SimpleStateMachineTests : Test
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task DisposeRollsBackCompletedSnapshot(bool preservePublishedSnapshot)
    {
        var location = GetTempPath();
        var stateMachine = new SnapshotStateMachine(new(Path.Combine(location, "snapshots")));
        try
        {
            FileWriter pendingWriter;
            await using (var wal = new WriteAheadLog(new() { Location = location }, stateMachine))
            {
                await wal.InitializeAsync(TestToken);
                await AppendAndApplyAsync(wal);
                pendingWriter = await stateMachine.WaitForWriterAsync();
                await WaitForWriteAsync(pendingWriter);

                if (preservePublishedSnapshot)
                {
                    await AppendAndApplyAsync(wal);
                    Equal(1L, stateMachine.As<ISnapshotManager>().Snapshot?.Index);
                    await AppendAndApplyAsync(wal);
                    pendingWriter = await stateMachine.WaitForWriterAsync();
                    await WaitForWriteAsync(pendingWriter);
                }
            }

            await stateMachine.DisposeAsync();
            await stateMachine.DisposeAsync();
            Throws<ObjectDisposedException>(pendingWriter.FlushToDisk);
            Empty(Directory.EnumerateFiles(stateMachine.Location.FullName, "*.tmp"));

            if (preservePublishedSnapshot)
            {
                Equal(1L, stateMachine.As<ISnapshotManager>().Snapshot?.Index);
                var bytes = await File.ReadAllBytesAsync(Path.Combine(stateMachine.Location.FullName, "1-1"), TestToken);
                Equal(42L, BinaryPrimitives.ReadInt64LittleEndian(bytes));
            }
            else
            {
                Null(((ISnapshotManager)stateMachine).Snapshot);
                Empty(stateMachine.Location.EnumerateFiles());
            }
        }
        finally
        {
            await stateMachine.DisposeAsync();
            stateMachine.DisposeCapturedWriters();
            Directory.Delete(location, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task DisposeSuppressesSnapshotFailure(bool cancel)
    {
        var location = GetTempPath();
        var stateMachine = new SnapshotStateMachine(new(location)) { Cancel = cancel, Fail = !cancel };
        try
        {
            await ((IStateMachine)stateMachine).ApplyAsync(new LogEntry(1L, 1L), TestToken);
            var writer = await stateMachine.WaitForWriterAsync();
            await stateMachine.DisposeAsync().AsTask().WaitAsync(DefaultTimeout, TestToken);
            Throws<ObjectDisposedException>(writer.FlushToDisk);
            Empty(stateMachine.Location.EnumerateFiles());
        }
        finally
        {
            await stateMachine.DisposeAsync();
            stateMachine.DisposeCapturedWriters();
            Directory.Delete(location, recursive: true);
        }
    }

    [Fact]
    public static async Task DisposeWithoutSnapshot()
    {
        var location = GetTempPath();
        var stateMachine = new SnapshotStateMachine(new(location));
        await stateMachine.DisposeAsync();
        await stateMachine.DisposeAsync();
        Empty(stateMachine.Location.EnumerateFiles());
        Directory.Delete(location);
    }

    private static async Task AppendAndApplyAsync(WriteAheadLog wal)
    {
        var index = await ((IPersistentState)wal).AppendAsync(new BinaryLogEntry { Term = 1L, Content = new byte[] { 1 } }, TestToken);
        await wal.CommitAsync(index, TestToken);
        await wal.WaitForApplyAsync(index, TestToken).AsTask().WaitAsync(DefaultTimeout, TestToken);
    }

    private static async Task WaitForWriteAsync(FileWriter writer)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        timeout.CancelAfter(DefaultTimeout);
        // FilePosition advances only after the OS write succeeds; cancellation can no longer abort that write.
        while (writer.FilePosition < sizeof(long))
            await Task.Delay(1, timeout.Token);
    }

    private sealed class SnapshotStateMachine(DirectoryInfo location) : SimpleStateMachine(location)
    {
        private readonly Channel<FileWriter> writers = Channel.CreateUnbounded<FileWriter>();
        private readonly List<FileWriter> capturedWriters = [];

        internal DirectoryInfo Location { get; } = location;
        internal bool Cancel { get; init; }
        internal bool Fail { get; init; }

        internal Task<FileWriter> WaitForWriterAsync()
            => writers.Reader.ReadAsync(TestToken).AsTask().WaitAsync(DefaultTimeout, TestToken);

        // Release handles even when running the regression against the unfixed implementation.
        internal void DisposeCapturedWriters()
        {
            foreach (var writer in capturedWriters)
                writer.Dispose();
        }

        protected override ValueTask<bool> ApplyAsync(LogEntry entry, CancellationToken token)
            => ValueTask.FromResult(entry.Index % 2L is 1L);

        protected override ValueTask RestoreAsync(FileInfo snapshotFile, CancellationToken token)
            => ValueTask.CompletedTask;

        protected override async ValueTask PersistAsync(IAsyncBinaryWriter writer, CancellationToken token)
        {
            var fileWriter = IsType<FileWriter>(writer, exactMatch: false);
            capturedWriters.Add(fileWriter);
            await writer.WriteLittleEndianAsync(42L, token);
            True(writers.Writer.TryWrite(fileWriter));
            if (Cancel)
                await Task.Delay(Timeout.Infinite, token);
            if (Fail)
                throw new IOException();
        }
    }
}
