using System.Runtime;

namespace DotNext.Runtime;

public sealed class GCLatencyModeScopeTests : Test
{
    [Fact]
    public static void EnableDisableMode()
    {
        using (GCLatencyModeScope.SustainedLowLatency)
        {
            Equal(GCLatencyMode.SustainedLowLatency, GCSettings.LatencyMode);
        }
    }
}