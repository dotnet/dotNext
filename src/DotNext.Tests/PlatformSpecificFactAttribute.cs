public sealed class PlatformSpecificFactAttribute : FactAttribute
{
    public PlatformSpecificFactAttribute(params string[] supportedPlatforms)
    {
        if (!Array.Exists(supportedPlatforms, OperatingSystem.IsOSPlatform))
            Skip = "Unsupported platform";
    }
}