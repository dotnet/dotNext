using System.CommandLine;

namespace DotNext.Maintenance.CommandLine;

/// <summary>
/// Represents application maintenance command.
/// </summary>
/// <remarks>
/// All registered singleton services of this type in DI container will be automatically
/// discovered by <see cref="CommandLineMaintenanceInterfaceHost"/>.
/// </remarks>
public sealed partial class ApplicationMaintenanceCommand : Command
{
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