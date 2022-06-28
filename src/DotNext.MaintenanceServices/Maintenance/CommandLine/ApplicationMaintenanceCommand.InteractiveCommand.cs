using System.Buffers;
using System.CommandLine;

namespace DotNext.Maintenance.CommandLine;

using DefaultBindings = Binding.DefaultBindings;

public partial class ApplicationMaintenanceCommand
{
    /// <summary>
    /// Creates a command that allows to enter interactive mode.
    /// </summary>
    /// <returns>A new command.</returns>
    public static ApplicationMaintenanceCommand EnterInteractiveModeCommand()
    {
        var command = new ApplicationMaintenanceCommand("interactive-mode", CommandResources.InteractiveCommandDescription);
        command.SetHandler(EnterInteractiveMode, DefaultBindings.Session);
        return command;

        static void EnterInteractiveMode(IMaintenanceSession session)
        {
            session.IsInteractive = true;
            session.Output.Write(CommandResources.WelcomeMessage(RootCommand.ExecutableName) + Environment.NewLine);
        }
    }

    /// <summary>
    /// Creates a command that allows to leave interactive mode.
    /// </summary>
    /// <returns>A new command.</returns>
    public static ApplicationMaintenanceCommand LeaveInteractiveModeCommand()
    {
        var command = new ApplicationMaintenanceCommand("exit", CommandResources.ExitCommandDescription);
        command.SetHandler(static session => session.IsInteractive = false, DefaultBindings.Session);
        return command;
    }
}