namespace DotNext;

using Patterns;

internal sealed class Sentinel : ISingleton<Sentinel>
{
    public static Sentinel Instance { get; } = new();

    private Sentinel()
    {
    }
}