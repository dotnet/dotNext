using System.CommandLine.Invocation;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine.Authentication;

using IMaintenanceConsole = IO.IMaintenanceConsole;
using AnonymousPrincipal = Security.Principal.AnonymousPrincipal;

internal static class AuthenticationMiddleware
{
    private const int ForbiddenExitCode = 77; // EX_NOPERM from sysexits.h

    internal static async Task AuthenticateAsync(this IAuthenticationHandler handler, InvocationContext context, Func<InvocationContext, Task> next)
    {
        var session = (context.Console as IMaintenanceConsole)?.Session;

        if (session?.Principal is { Identity: { IsAuthenticated: true } })
        {
            // already authenticated
            await next(context).ConfigureAwait(false);
        }
        else if (session is not null && await handler.ChallengeAsync(context, session.Identity, context.GetCancellationToken()).ConfigureAwait(false) is { Identity: { IsAuthenticated: true } } principal)
        {
            // save authentication result
            session.Principal = principal;
            await next(context).ConfigureAwait(false);
        }
        else
        {
            // report error
            Forbid(context);
        }
    }

    internal static Task SetDefaultPrincipal(InvocationContext context, Func<InvocationContext, Task> next)
    {
        if ((context.Console as IMaintenanceConsole)?.Session is { Principal: null } session)
        {
            session.Principal = new GenericPrincipal(session.Identity, roles: null);
        }

        return next(context);
    }

    private static void Forbid(InvocationContext context)
    {
        context.ExitCode = ForbiddenExitCode;
        context.Console.Error.Write(CommandResources.AccessDenined);
        context.Console.Error.Write(Environment.NewLine);
    }
}