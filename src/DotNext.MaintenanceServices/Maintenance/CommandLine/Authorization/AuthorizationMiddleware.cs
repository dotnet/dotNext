using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Runtime.CompilerServices;

namespace DotNext.Maintenance.CommandLine.Authorization;

using static Buffers.BufferHelpers;
using IMaintenanceConsole = IO.IMaintenanceConsole;

internal static class AuthorizationMiddleware
{
    private const int ForbiddenExitCode = 77; // EX_NOPERM from sysexits.h

    internal static async Task AuthorizeAsync(this AuthorizationCallback? globalAuth, InvocationContext context, Func<InvocationContext, Task> next)
    {
        if (context.Console is not IMaintenanceConsole console || await AuthorizeCommandAsync(console, context).ConfigureAwait(false) && await globalAuth.AuthorizeAsync(console.Session, context.ParseResult.CommandResult, context.GetCancellationToken()).ConfigureAwait(false))
        {
            await next(context).ConfigureAwait(false);
        }
        else
        {
            console.Error.WriteString($"{CommandResources.AccessDenined}{Environment.NewLine}");
            context.ExitCode = ForbiddenExitCode;
            console.Session.IsInteractive = false;
        }
    }

    private static ValueTask<bool> AuthorizeCommandAsync(IMaintenanceConsole console, InvocationContext context)
        => context.ParseResult.CommandResult.Command is ApplicationMaintenanceCommand maintenanceCommand ? maintenanceCommand.AuthorizeAsync(console.Session, context.ParseResult.CommandResult, context.GetCancellationToken()) : new(true);

    [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    internal static async ValueTask<bool> AuthorizeAsync(this AuthorizationCallback? authorizationRules, IMaintenanceSession session, CommandResult target, CancellationToken token)
    {
        if (session.Principal is null)
            return false;

        foreach (AuthorizationCallback rule in authorizationRules?.GetInvocationList() ?? Array.Empty<AuthorizationCallback>())
        {
            if (!await rule.Invoke(session.Principal, target, session.Context, token).ConfigureAwait(false))
                return false;
        }

        return true;
    }
}