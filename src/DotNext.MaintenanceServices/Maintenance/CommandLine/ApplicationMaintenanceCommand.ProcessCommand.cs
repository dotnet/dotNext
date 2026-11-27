using System.CommandLine;

namespace DotNext.Maintenance.CommandLine;

partial class ApplicationMaintenanceCommand
{
    /// <summary>
    /// Creates a command which displays information about the current process.
    /// </summary>
    /// <returns>The command instance.</returns>
    public static ApplicationMaintenanceCommand ProcessCommand()
    {
        var command = new ApplicationMaintenanceCommand("process")
        {
            ShowCurrentProcessId(),
            ShowRuntimeVersion(),
        };

        command.Description = CommandResources.ProcessCommandDescription;
        return command;
    }

    private static ApplicationMaintenanceCommand ShowCurrentProcessId()
    {
        var command = new ApplicationMaintenanceCommand("id")
        {
            Description = CommandResources.ProcessCommandIdDescription,
        };

        command.SetAction(Invoke);
        return command;

        static Task Invoke(ParseResult result, CancellationToken token)
            => result.InvocationConfiguration.Output.WriteAsync(
                Environment.ProcessId.ToString().AsMemory(),
                token);
    }

    private static ApplicationMaintenanceCommand ShowRuntimeVersion()
    {
        var command = new ApplicationMaintenanceCommand("runtime")
        {
            Description = CommandResources.ProcessCommandRuntimeDescription,
        };
        
        command.SetAction(Invoke);
        return command;

        static Task Invoke(ParseResult result, CancellationToken token)
            => result.InvocationConfiguration.Output.WriteAsync(
                Environment.Version.ToString().AsMemory(), token);
    }
}