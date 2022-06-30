using System.Runtime.Versioning;
using System.Security.Principal;

namespace DotNext.Security.Principal;

/// <summary>
/// Represents identity of the peer interacting via Unix Domain Socket on Linux.
/// </summary>
/// <remarks>
/// This class is supported on Linux systems only.
/// </remarks>
[CLSCompliant(false)]
public sealed record class LinuxUdsPeerIdentity : IIdentity
{
    [SupportedOSPlatform("linux")]
    internal LinuxUdsPeerIdentity()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException();
    }

    /// <summary>
    /// Gets a value that indicates whether the user has been authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets calling process ID.
    /// </summary>
    public uint ProcessId { get; internal init; }

    /// <summary>
    /// Gets user ID of the process identified by <see cref="ProcessId"/>.
    /// </summary>
    public uint UserId { get; internal init; }

    /// <summary>
    /// Gets group ID of the process identified by <see cref="ProcessId"/>.
    /// </summary>
    public uint GroupId { get; internal init; }

    /// <inheritdoc />
    string? IIdentity.AuthenticationType => "ucred";

    /// <inheritdoc />
    string? IIdentity.Name => null;
}