using System.Diagnostics.CodeAnalysis;

namespace DotNext;

internal static class LibrarySettings
{
    private const string UseNativeAllocationFeature = "DotNext.Buffers.NativeAllocation";
    private const string UseRandomStringInternalBufferCleanupFeature = "DotNext.Security.RandomStringInternalBufferCleanup";
    
    internal static int StackallocThreshold
    {
        get
        {
            const string environmentVariableName = "DOTNEXT_STACK_ALLOC_THRESHOLD";
            const string configurationParameterName = "DotNext.Buffers.StackAllocThreshold";
            const int defaultValue = 511;
            const int minimumValue = 14;

            if (AppContext.GetData(configurationParameterName) is not int result)
            {
                int.TryParse(Environment.GetEnvironmentVariable(environmentVariableName), out result);
            }

            return result > minimumValue ? result : defaultValue;
        }
    }
    
    private static bool IsSupported([ConstantExpected] string featureName)
        => !AppContext.TryGetSwitch(featureName, out var result) || result;

    // TODO: [FeatureSwitchDefinition(UseRandomStringInternalBufferCleanupFeature)]
    internal static bool UseRandomStringInternalBufferCleanup
        => IsSupported(UseRandomStringInternalBufferCleanupFeature);

    // TODO: [FeatureSwitchDefinition(EnableNativeAllocationFeature)]
    internal static bool UseNativeAllocation
        => IsSupported(UseNativeAllocationFeature);
}