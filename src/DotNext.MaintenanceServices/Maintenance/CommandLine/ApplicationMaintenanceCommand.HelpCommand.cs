using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Maintenance.CommandLine;

partial class ApplicationMaintenanceCommand
{
    /// <summary>
    /// Represents help command.
    /// </summary>
    /// <returns>A new command instance.</returns>
    public static ApplicationMaintenanceCommand HelpCommand()
    {
        var command = new ApplicationMaintenanceCommand("help", CommandResources.HelpCommandDescription);
        command.SetHandler(Handle);
        return command;

        static void Handle(InvocationContext context)
        {
            if (context.BindingContext.GetService<HelpBuilder>() is { } builder)
            {
                using var output = context.BindingContext.Console.Out.CreateTextWriter();
                var helpContext = new HelpContext(builder,
                    context.Parser.Configuration.RootCommand,
                    output,
                    context.ParseResult);

                builder.Write(helpContext);
            }
        }
    }
}