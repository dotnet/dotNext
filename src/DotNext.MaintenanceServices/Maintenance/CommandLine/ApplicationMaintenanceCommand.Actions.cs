using System.CommandLine;
using System.CommandLine.Invocation;

namespace DotNext.Maintenance.CommandLine;

partial class ApplicationMaintenanceCommand
{
    /// <summary>
    /// Represents synchronous command handler.
    /// </summary>
    public abstract class SynchronousAction : SynchronousCommandLineAction
    {
        /// <summary>
        /// Invokes the maintenance command.
        /// </summary>
        /// <param name="session">The maintenance session.</param>
        /// <param name="result">The parsing result.</param>
        /// <returns>Exit code.</returns>
        protected abstract int Invoke(IMaintenanceSession session, ParseResult result);

        /// <inheritdoc />
        public sealed override int Invoke(ParseResult result)
            => result.Configuration is CommandContext context ? Invoke(context.Session, result) : -1;
    }
    
    private sealed class DelegatingSynchronousAction(Action<IMaintenanceSession, ParseResult> action) : SynchronousAction
    {
        protected override int Invoke(IMaintenanceSession session, ParseResult result)
        {
            action(session, result);
            return 0;
        }
    }
    
    /// <summary>
    /// Represents asynchronous command handler.
    /// </summary>
    public abstract class AsynchronousAction : AsynchronousCommandLineAction
    {
        /// <summary>
        /// Invokes the maintenance command.
        /// </summary>
        /// <param name="session">The maintenance session.</param>
        /// <param name="result">The parsing result.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Exit code.</returns>
        protected abstract Task<int> InvokeAsync(IMaintenanceSession session, ParseResult result, CancellationToken token);

        /// <inheritdoc />
        public override Task<int> InvokeAsync(ParseResult result, CancellationToken token = default)
            => result.Configuration is CommandContext context ? InvokeAsync(context.Session, result, token) : Task.FromResult(-1);
    }

    private sealed class DelegatingAsynchronousAction(Func<IMaintenanceSession, ParseResult, CancellationToken, ValueTask> action)
        : AsynchronousAction
    {
        protected override async Task<int> InvokeAsync(IMaintenanceSession session, ParseResult result, CancellationToken token)
        {
            await action(session, result, token).ConfigureAwait(false);
            return 0;
        }
    }

    /// <summary>
    /// Sets synchronous command handler.
    /// </summary>
    /// <param name="handler">The command handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public void SetAction(Action<IMaintenanceSession, ParseResult> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Action = new DelegatingSynchronousAction(handler);
    }

    /// <summary>
    /// Sets asynchronous command handler.
    /// </summary>
    /// <param name="handler">The command handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public void SetAction(Func<IMaintenanceSession, ParseResult, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Action = new DelegatingAsynchronousAction(handler);
    }
}