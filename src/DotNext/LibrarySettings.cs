namespace DotNext;

internal static class LibrarySettings
{
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

    internal static bool DisableRandomStringInternalBufferCleanup
    {
        get
        {
            const string switchName = "DotNext.Security.DisableRandomStringInternalBufferCleanup";
            const bool defaultValue = false;

            if (!AppContext.TryGetSwitch(switchName, out var result))
                result = defaultValue;

            return result;
        }
    }

    internal static bool DisableNativeAllocation
    {
        get
        {
            const string switchName = "DotNext.Buffers.DisableNativeAllocation";
            const bool defaultValue = false;

            if (!AppContext.TryGetSwitch(switchName, out var result))
                result = defaultValue;

            return result;
        }
    }
}