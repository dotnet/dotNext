namespace DotNext.IO.Hashing;

/// <summary>
/// Represents FNV-1a 64-bit hash algorithm.
/// </summary>
public sealed class FNV1a32(bool salted = false) : FNV1a<int, FNV1aParameters>(salted)
{
    internal static int Hash<T>(T[] array, bool salted)
        where T : unmanaged
        => Hash(new ReadOnlySpan<T>(array), salted);
}