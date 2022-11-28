using System.Diagnostics.CodeAnalysis;
using System.Runtime;

namespace DotNext.Runtime
{
    [ExcludeFromCodeCoverage]
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
}