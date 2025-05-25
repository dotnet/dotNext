namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

public partial class CommandInterpreter
{
    /// <summary>
    /// Indicates that the method represents command handler.
    /// </summary>
    /// <remarks>
    /// The marked method must have the following signature:
    /// <code>
    /// [CommandHandler]
    /// public async ValueTask MyHandler(MyCommand command, CancellationToken token)
    /// {
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    protected sealed class CommandHandlerAttribute : Attribute;
}