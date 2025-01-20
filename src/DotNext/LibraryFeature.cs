using System.Diagnostics.CodeAnalysis;

namespace DotNext;

internal static class LibraryFeature
{
    internal static bool IsSupported([ConstantExpected] string featureName)
        => !AppContext.TryGetSwitch(featureName, out var result) || result;
}