using System.CommandLine.Invocation;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine;

using LinuxUdsPeerIdentity = Security.Principal.LinuxUdsPeerIdentity;

/// <summary>
/// Represents authentication handler that utilizes identity of the remote peer connected to the Unix Domain Socket.
/// </summary>
[SupportedOSPlatform("linux")]
[CLSCompliant(false)]
public abstract class LinuxUdsPeerAuthenticationHandler : IAuthenticationHandler
{
    /// <inheritdoc />
    ValueTask<IPrincipal?> IAuthenticationHandler.ChallengeAsync(InvocationContext context, IIdentity identity, CancellationToken token)
        => identity is LinuxUdsPeerIdentity peerIdentity ? ChallengeAsync(peerIdentity, token) : new(default(IPrincipal?));

    /// <summary>
    /// Authenticates maintenance session.
    /// </summary>
    /// <param name="identity">The identity of the remote process connected to the Unix Domain Socket.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Authentication result; or <see langword="null"/> in case of failed authentication.</returns>
    protected abstract ValueTask<IPrincipal?> ChallengeAsync(LinuxUdsPeerIdentity identity, CancellationToken token);
}