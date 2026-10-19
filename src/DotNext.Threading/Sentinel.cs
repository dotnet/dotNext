using System.Runtime.CompilerServices;

namespace DotNext;

internal static class Sentinel
{
    public static object Instance => GetInstance();
    
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = nameof(Instance))]
    private static extern ref readonly object GetInstance(
        [UnsafeAccessorType("DotNext.Sentinel, DotNext")]
        object? obj = null);
}