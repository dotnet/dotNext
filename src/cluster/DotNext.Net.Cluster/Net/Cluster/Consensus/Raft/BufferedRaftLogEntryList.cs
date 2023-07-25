using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;

/// <summary>
/// Represents log entry producer that allows to bufferize log entries
/// from another producer.
/// </summary>
/// <remarks>
/// This type is intended for developing transport-layer buffering
/// and low level I/O optimizations when writing custom Write-Ahead Log.
/// It's not recommended to use the type in the application code.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public readonly struct BufferedRaftLogEntryList : IDisposable, IReadOnlyList<BufferedRaftLogEntry>
{
    private readonly BufferedRaftLogEntry[]? entries;

    private BufferedRaftLogEntryList(BufferedRaftLogEntry[] entries)
        => this.entries = entries;

    private BufferedRaftLogEntry[] Entries => entries ?? Array.Empty<BufferedRaftLogEntry>();

    /// <summary>
    /// Creates asynchronous log entry producer from this list.
    /// </summary>
    /// <returns>The entry producer.</returns>
    public ILogEntryProducer<BufferedRaftLogEntry> ToProducer() => new LogEntryProducer<BufferedRaftLogEntry>(Entries);

    /// <summary>
    /// Gets buffered log entry by the index.
    /// </summary>
    /// <param name="index">The index of the log entry.</param>
    public ref readonly BufferedRaftLogEntry this[int index] => ref Entries[index];

    /// <inheritdoc />
    BufferedRaftLogEntry IReadOnlyList<BufferedRaftLogEntry>.this[int index] => this[index];

    /// <summary>
    /// Gets the number of buffered log entries.
    /// </summary>
    public int Count => Entries.Length;

    /// <summary>
    /// Constructs bufferized copy of all log entries presented in the sequence.
    /// </summary>
    /// <param name="producer">The sequence of log entries to be copied.</param>
    /// <param name="options">Buffering options.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TEntry">The type of the entry in the source sequence.</typeparam>
    /// <returns>The copy of the log entries.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task<BufferedRaftLogEntryList> CopyAsync<TEntry>(ILogEntryProducer<TEntry> producer, RaftLogEntriesBufferingOptions options, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
    {
        var entries = new BufferedRaftLogEntry[producer.RemainingCount];
        long bufferedBytes = 0L;
        for (nint index = 0; await producer.MoveNextAsync().ConfigureAwait(false); index++)
        {
            var current = producer.Current;
            var buffered = await (bufferedBytes < options.MemoryLimit ?
                BufferedRaftLogEntry.CopyAsync(current, options, token) :
                BufferedRaftLogEntry.CopyToFileAsync(current, options, current.Length, token)).ConfigureAwait(false);

            entries[index] = buffered;

            if (buffered.InMemory)
                bufferedBytes += buffered.Length;
        }

        return new BufferedRaftLogEntryList(entries);
    }

    /// <summary>
    /// Constructs bufferized copy of all log entries presented in the list.
    /// </summary>
    /// <param name="list">The list of log entries to be copied.</param>
    /// <param name="options">Buffering options.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TEntry">The type of the entry in the source sequence.</typeparam>
    /// <typeparam name="TList">The type of the list of log entries.</typeparam>
    /// <returns>The copy of the log entries.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task<BufferedRaftLogEntryList> CopyAsync<TEntry, TList>(TList list, RaftLogEntriesBufferingOptions options, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
        where TList : notnull, IReadOnlyList<TEntry>
    {
        var entries = new BufferedRaftLogEntry[list.Count];
        long bufferedBytes = 0L;
        for (var index = 0; index < list.Count; index++)
        {
            var current = list[index];
            var buffered = await (bufferedBytes < options.MemoryLimit ?
                BufferedRaftLogEntry.CopyAsync(current, options, token) :
                BufferedRaftLogEntry.CopyToFileAsync(current, options, current.Length, token)).ConfigureAwait(false);

            entries[index] = buffered;

            if (buffered.InMemory)
                bufferedBytes += buffered.Length;
        }

        return new(entries);
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => Entries.GetEnumerator();

    /// <inheritdoc />
    IEnumerator<BufferedRaftLogEntry> IEnumerable<BufferedRaftLogEntry>.GetEnumerator()
        => Entries.AsEnumerable().GetEnumerator();

    /// <summary>
    /// Releases all buffered log entries.
    /// </summary>
    public void Dispose()
    {
        foreach (ref var entry in entries.AsSpan())
        {
            entry.Dispose();
            entry = default;
        }
    }
}