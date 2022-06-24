using System.Buffers;
using System.CommandLine;

namespace DotNext.Maintenance.CommandLine;

using DefaultBindings = Binding.DefaultBindings;

public static partial class DefaultCommands
{
    /// <summary>
    /// Creates a command that allows to enter interactive mode.
    /// </summary>
    /// <returns>A new command.</returns>
    public static ApplicationManagementCommand EnterInteractiveModeCommand()
    {
        var command = new ApplicationManagementCommand("interactive-mode", CommandResources.InteractiveCommandDescription);
        command.SetHandler(EnterInteractiveMode, DefaultBindings.Session);
        return command;

        static void EnterInteractiveMode(IManagementSession session)
        {
            session.IsInteractive = true;
            session.Output.Write(CommandResources.WelcomeMessage(RootCommand.ExecutableName) + Environment.NewLine);
        }
    }

    /// <summary>
    /// Creates a command that allows to leave interactive mode.
    /// </summary>
    /// <returns>A new command.</returns>
    public static ApplicationManagementCommand LeaveInteractiveModeCommand()
    {
        var command = new ApplicationManagementCommand("exit", CommandResources.ExitCommandDescription);
        command.SetHandler(static session => session.IsInteractive = false, DefaultBindings.Session);
        return command;
    }
}