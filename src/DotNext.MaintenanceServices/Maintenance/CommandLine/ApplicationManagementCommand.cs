using System.CommandLine;

namespace DotNext.Maintenance.CommandLine;

/// <summary>
/// Represents application management command.
/// </summary>
/// <remarks>
/// All registered singleton services of this type in DI container will be automatically
/// discovered by <see cref="CommandLineManagementInterfaceHost"/>.
/// </remarks>
public sealed class ApplicationManagementCommand : Command
{
    /// <summary>
    /// Initializes a new management command.
    /// </summary>
    /// <param name="name">The name of the command.</param>
    /// <param name="description">The description of the command.</param>
    public ApplicationManagementCommand(string name, string? description = null)
        : base(name, description)
    {
    }
}