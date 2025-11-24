using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext;

[ExcludeFromCodeCoverage]
public sealed class PlatformSpecificFactAttribute : FactAttribute
{
    public PlatformSpecificFactAttribute(string[] supportedPlatforms,
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!Array.Exists(supportedPlatforms, OperatingSystem.IsOSPlatform))
            Skip = "Unsupported platform";
    }

    public PlatformSpecificFactAttribute(string supportedPlatform,
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : this([supportedPlatform], sourceFilePath, sourceLineNumber)
    {
    }
}