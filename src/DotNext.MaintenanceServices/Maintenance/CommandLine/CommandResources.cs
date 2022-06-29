using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext.Maintenance.CommandLine;

using static Resources.ResourceManagerExtensions;

[ExcludeFromCodeCoverage]
internal static class CommandResources
{
    private static readonly ResourceManager Resources = new("DotNext.Maintenance.CommandLine.CommandResources", Assembly.GetExecutingAssembly());

    internal static string WelcomeMessage(string appName)
        => Resources.Get().Format(appName);

    internal static string CommandTimeoutOccurred => (string)Resources.Get();

    internal static string GCCommandDescription => (string)Resources.Get();

    internal static string GCCollectCommandDescription => (string)Resources.Get();

    internal static string GCCollectCommandInvalidGenerationArg(string? generation)
        => Resources.Get().Format(generation);

    internal static string GCCollectCommandGenerationArgDescription => (string)Resources.Get();

    internal static string GCCollectCommandBlockingOptionDescription => (string)Resources.Get();

    internal static string GCCollectCommandCompactingOptionDescription => (string)Resources.Get();

    internal static string GCLohModeCommandDescription => (string)Resources.Get();

    internal static string GCLohModeCommandModeArgDescription => (string)Resources.Get();

    internal static string GCLohModeCommandInvalidModeArg(string? mode)
        => Resources.Get().Format(mode);

    internal static string InteractiveCommandDescription => (string)Resources.Get();

    internal static string ExitCommandDescription => (string)Resources.Get();

    internal static string ProbeCommandDescription => (string)Resources.Get();

    internal static string ProbeCommandProbeTypeArgDescription => (string)Resources.Get();

    internal static string ProbeCommandTimeoutArgDescription => (string)Resources.Get();

    internal static string ProbeCommandInvalidTimeoutArg(string? timeout)
        => Resources.Get().Format(timeout);

    internal static string ProbeCommandSuccessfulResponseOptionDescription => (string)Resources.Get();

    internal static string ProbeCommandUnsuccessfulResponseOptionDescription => (string)Resources.Get();
}