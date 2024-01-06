using System.Security.Principal;

namespace DotNext.Security.Principal;

using Patterns;

internal sealed class AnonymousPrincipal : IPrincipal, IIdentity, ISingleton<AnonymousPrincipal>
{
    public static AnonymousPrincipal Instance { get; } = new();

    IIdentity IPrincipal.Identity => this;

    bool IPrincipal.IsInRole(string role) => false;

    string IIdentity.Name => "anonymous";

    bool IIdentity.IsAuthenticated => false;

    string? IIdentity.AuthenticationType => null;
}