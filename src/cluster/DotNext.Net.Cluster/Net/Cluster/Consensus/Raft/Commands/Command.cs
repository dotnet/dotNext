namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

/// <summary>
/// Represents extension methods to work with commands.
/// </summary>
public static class Command
{
    /// <summary>
    /// Appends a strongly typed command to the log tail.
    /// </summary>
    /// <param name="state">The log.</param>
    /// <param name="command">The command to append.</param>
    /// <param name="context">The optional context to be passed to the state machine.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    /// <returns>The index of the added command within the log.</returns>
    public static ValueTask<long> AppendAsync<TCommand>(this IPersistentState state, TCommand command, object? context = null,
        CancellationToken token = default)
        where TCommand : ICommand<TCommand>
        => state.AppendAsync<LogEntry<TCommand>>(new() { Command = command, Term = state.Term, Context = context }, token);
}