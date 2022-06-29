using System.Security.Principal;

namespace DotNext.Security.Principal;

internal sealed class AnonymousPrincipal : IPrincipal, IIdentity
{
    internal static readonly AnonymousPrincipal Instance = new();

    IIdentity IPrincipal.Identity => this;

    bool IPrincipal.IsInRole(string role) => false;

    string IIdentity.Name => "anonymous";

    bool IIdentity.IsAuthenticated => false;

    string? IIdentity.AuthenticationType => null;
}