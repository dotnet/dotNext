using System.Runtime;

namespace DotNext.Runtime;

public sealed class GCLatencyModeScopeTests : Test
{
    [Fact]
    public static void EnableDisableMode()
    {
        using (GCLatencyMode.SustainedLowLatency.Enable())
        {
            Equal(GCLatencyMode.SustainedLowLatency, GCSettings.LatencyMode);
        }
    }
}