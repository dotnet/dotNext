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
    internal readonly ArraySegment<BufferedRaftLogEntry> Entries;

    internal BufferedRaftLogEntryList(ArraySegment<BufferedRaftLogEntry> entries)
        => Entries = entries;

    /// <summary>
    /// Creates asynchronous log entry producer from this list.
    /// </summary>
    /// <returns>The entry producer.</returns>
    public ILogEntryProducer<BufferedRaftLogEntry> ToProducer() => new LogEntryProducer<BufferedRaftLogEntry>(Entries);

    /// <summary>
    /// Gets buffered log entry by the index.
    /// </summary>
    /// <param name="index">The index of the log entry.</param>
    public ref readonly BufferedRaftLogEntry this[int index] => ref Entries.AsSpan()[index];

    /// <inheritdoc />
    BufferedRaftLogEntry IReadOnlyList<BufferedRaftLogEntry>.this[int index] => this[index];

    /// <summary>
    /// Gets the number of buffered log entries.
    /// </summary>
    public int Count => Entries.Count;

    /// <summary>
    /// Constructs bufferized copy of all log entries presented in the sequence.
    /// </summary>
    /// <param name="producer">The sequence of log entries to be copied.</param>
    /// <param name="options">Buffering options.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TEntry">The type of the entry in the source sequence.</typeparam>
    /// <returns>The copy of the log entries.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static Task<BufferedRaftLogEntryList> CopyAsync<TEntry>(ILogEntryProducer<TEntry> producer, RaftLogEntriesBufferingOptions options, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
    {
        return CreateListAsync<IAsyncEnumerator<TEntry>>(BufferizeAsync, producer, producer.RemainingCount, options, token);

        static async IAsyncEnumerator<BufferedRaftLogEntry> BufferizeAsync(IAsyncEnumerator<TEntry> enumerator, RaftLogEntriesBufferingOptions options, CancellationToken token)
        {
            for (var bufferedBytes = 0L; await enumerator.MoveNextAsync().ConfigureAwait(false);)
            {
                var buffered = await BufferizeAsync<TEntry>(enumerator.Current, options, bufferedBytes, token).ConfigureAwait(false);

                if (buffered.InMemory)
                    bufferedBytes += buffered.Length;

                yield return buffered;
            }
        }
    }

    private static ValueTask<BufferedRaftLogEntry> BufferizeAsync<TEntry>(TEntry entry, RaftLogEntriesBufferingOptions options, long bufferedBytes, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
        => bufferedBytes < options.MemoryLimit ? BufferedRaftLogEntry.CopyAsync(entry, options, token) : BufferedRaftLogEntry.CopyToFileAsync(entry, options, token);

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
    public static Task<BufferedRaftLogEntryList> CopyAsync<TEntry, TList>(TList list, RaftLogEntriesBufferingOptions options, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
        where TList : notnull, IReadOnlyList<TEntry>
        => CreateListAsync(BufferizeAsync<TEntry, TList>, list, list.Count, options, token);

    internal static async IAsyncEnumerator<BufferedRaftLogEntry> BufferizeAsync<TEntry, TList>(TList list, RaftLogEntriesBufferingOptions options, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
        where TList : notnull, IReadOnlyList<TEntry>
    {
        var bufferedBytes = 0L;
        for (var i = 0; i < list.Count; i++)
        {
            var buffered = await BufferizeAsync(list[i], options, bufferedBytes, token).ConfigureAwait(false);

            if (buffered.InMemory)
                bufferedBytes += buffered.Length;

            yield return buffered;
        }
    }

    private static Task<BufferedRaftLogEntryList> CreateListAsync<TArg>(Generator<TArg> generator, TArg arg, long count, RaftLogEntriesBufferingOptions options, CancellationToken token)
    {
        return CopyAsync(generator(arg, options, token), new BufferedRaftLogEntry[count]);

        static async Task<BufferedRaftLogEntryList> CopyAsync(IAsyncEnumerator<BufferedRaftLogEntry> source, BufferedRaftLogEntry[] destination)
        {
            try
            {
                for (nuint i = 0; await source.MoveNextAsync().ConfigureAwait(false); i++)
                {
                    destination[i] = source.Current;
                }
            }
            finally
            {
                await source.DisposeAsync().ConfigureAwait(false);
            }

            return new(destination);
        }
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
        foreach (ref var entry in Entries.AsSpan())
        {
            entry.Dispose();
            entry = default;
        }
    }

    internal delegate IAsyncEnumerator<BufferedRaftLogEntry> Generator<TArg>(TArg arg, RaftLogEntriesBufferingOptions options, CancellationToken token);
}