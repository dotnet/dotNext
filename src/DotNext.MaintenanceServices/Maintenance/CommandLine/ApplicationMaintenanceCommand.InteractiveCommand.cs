using System.CommandLine;

namespace DotNext.Maintenance.CommandLine;

partial class ApplicationMaintenanceCommand
{
    /// <summary>
    /// Creates a command that allows to enter interactive mode.
    /// </summary>
    /// <returns>A new command.</returns>
    public static ApplicationMaintenanceCommand EnterInteractiveModeCommand()
    {
        var command = new ApplicationMaintenanceCommand("interactive-mode")
        {
            Description = CommandResources.InteractiveCommandDescription,
        };
        
        command.SetAction(EnterInteractiveMode);
        return command;

        static Task EnterInteractiveMode(ParseResult result, CancellationToken token)
        {
            if (result.Configuration is CommandContext context)
                context.Session.IsInteractive = true;

            return result.Configuration.Output.WriteLineAsync(
                CommandResources.WelcomeMessage(RootCommand.ExecutableName).AsMemory(),
                token);
        }
    }

    /// <summary>
    /// Creates a command that allows to leave interactive mode.
    /// </summary>
    /// <returns>A new command.</returns>
    public static ApplicationMaintenanceCommand LeaveInteractiveModeCommand()
    {
        var command = new ApplicationMaintenanceCommand("exit")
        {
            Description = CommandResources.ExitCommandDescription,
        };

        command.SetAction(static result =>
        {
            if (result.Configuration is CommandContext context)
                context.Session.IsInteractive = false;
        });
        
        return command;
    }
}