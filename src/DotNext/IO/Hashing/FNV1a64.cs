namespace DotNext.IO.Hashing;

/// <summary>
/// Represents FNV-1a 64-bit hash algorithm.
/// </summary>
public sealed class FNV1a64(bool salted = false) : FNV1a<long, FNV1aParameters>(salted)
{
}