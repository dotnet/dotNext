using System.Security.Principal;

namespace DotNext.Maintenance;

/// <summary>
/// Represents AMI interaction session.
/// </summary>
public interface IMaintenanceSession
{
    /// <summary>
    /// Gets or sets a value indicating that the current session is interactive.
    /// </summary>
    bool IsInteractive { get; set; }

    /// <summary>
    /// Gets a context that can be used to exchange information between command executions.
    /// </summary>
    IDictionary<string, object> Context { get; }

    /// <summary>
    /// Gets command response writer.
    /// </summary>
    TextWriter ResponseWriter { get; }

    /// <summary>
    /// Gets identity of the user started this session.
    /// </summary>
    /// <remarks>
    /// On Linux and FreeBSD, it is possible to obtain identity of the user
    /// started AMI session.
    /// </remarks>
    IIdentity Identity { get; }

    /// <summary>
    /// Gets or sets the user started this session.
    /// </summary>
    /// <value><see langword="null"/> means unauthenticated session.</value>
    IPrincipal? Principal { get; set; }
}