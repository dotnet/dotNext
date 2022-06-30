using System.CommandLine;
using System.CommandLine.Parsing;

namespace DotNext.Maintenance.CommandLine;

using Authorization;

/// <summary>
/// Represents application maintenance command.
/// </summary>
/// <remarks>
/// All registered singleton services of this type in DI container will be automatically
/// discovered by <see cref="CommandLineMaintenanceInterfaceHost"/>.
/// </remarks>
public sealed partial class ApplicationMaintenanceCommand : Command
{
    private AuthorizationCallback? authorizationRules;

    /// <summary>
    /// Initializes a new maintenance command.
    /// </summary>
    /// <param name="name">The name of the command.</param>
    /// <param name="description">The description of the command.</param>
    public ApplicationMaintenanceCommand(string name, string? description = null)
        : base(name, description)
    {
    }

    /// <summary>
    /// Allows to attach custom authorization rules for this command.
    /// </summary>
    public event AuthorizationCallback Authorization
    {
        add => authorizationRules += value;
        remove => authorizationRules -= value;
    }

    internal ValueTask<bool> AuthorizeAsync(IMaintenanceSession session, CommandResult target, CancellationToken token)
        => authorizationRules.AuthorizeAsync(session, target, token);

    /// <summary>
    /// Gets a collection of default commands.
    /// </summary>
    /// <returns>A collection of default commands.</returns>
    /// <seealso cref="GCCommand"/>
    /// <seealso cref="EnterInteractiveModeCommand"/>
    /// <seealso cref="LeaveInteractiveModeCommand"/>
    public static IEnumerable<ApplicationMaintenanceCommand> GetDefaultCommands()
    {
        yield return GCCommand();
        yield return EnterInteractiveModeCommand();
        yield return LeaveInteractiveModeCommand();
    }
}