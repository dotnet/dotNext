using System.CommandLine;

namespace DotNext.Maintenance.CommandLine;

/// <summary>
/// Represents a set of standard management commands.
/// </summary>
public static partial class DefaultCommands
{
    /// <summary>
    /// Adds default commands.
    /// </summary>
    /// <param name="command">The root command.</param>
    public static void AddDefaultCommands(this RootCommand command)
    {
        command.Add(GCCommand());
        command.Add(EnterInteractiveModeCommand());
        command.Add(LeaveInteractiveModeCommand());
    }
}