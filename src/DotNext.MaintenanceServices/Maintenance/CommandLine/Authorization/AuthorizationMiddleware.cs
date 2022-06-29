using System.Buffers;
using System.CommandLine.Invocation;

namespace DotNext.Maintenance.CommandLine.Authorization;

using IMaintenanceConsole = IO.IMaintenanceConsole;

internal static class AuthorizationMiddleware
{
    private const int ForbiddenExitCode = 77; // EX_NOPERM from sysexits.h

    internal static async Task AuthorizeAsync(InvocationContext context, Func<InvocationContext, Task> next)
    {
        if (context.Console is IMaintenanceConsole console && context.ParseResult.CommandResult.Command is ApplicationMaintenanceCommand command && !await command.AuthorizeAsync(console.Session.Principal, context.ParseResult.CommandResult, context.GetCancellationToken()).ConfigureAwait(false))
        {
            console.Error.Write(CommandResources.AccessDenined + Environment.NewLine);
            context.ExitCode = ForbiddenExitCode;
        }
        else
        {
            await next(context).ConfigureAwait(false);
        }
    }
}