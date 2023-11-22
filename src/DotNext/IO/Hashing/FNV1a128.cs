namespace DotNext.IO.Hashing;

/// <summary>
/// Represents FNV-1a 128-bit hash algorithm.
/// </summary>
public sealed class FNV1a128(bool salted = false) : FNV1a<Int128, FNV1aParameters>(salted)
{
}