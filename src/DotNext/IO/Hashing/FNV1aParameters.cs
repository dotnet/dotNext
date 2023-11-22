namespace DotNext.IO.Hashing;

/// <summary>
/// Represents parameters of FNV-1a hash algorithm for 32, 64, and 128 bits variations.
/// </summary>
public readonly struct FNV1aParameters : IFNV1aParameters<int>, IFNV1aParameters<long>, IFNV1aParameters<Int128>
{
    /// <summary>
    /// Gets offset basis for 32-bit version of FNV-1a hash algorithm.
    /// </summary>
    static int IFNV1aParameters<int>.Offset => unchecked((int)2166136261);

    /// <summary>
    /// Gets prime number for 32-bit version of FNV-1a hash algorithm.
    /// </summary>
    static int IFNV1aParameters<int>.Prime => 16777619;

    /// <summary>
    /// Gets offset basis for 64-bit version of FNV-1a hash algorithm.
    /// </summary>
    static long IFNV1aParameters<long>.Offset => unchecked((long)14695981039346656037);

    /// <summary>
    /// Gets prime number for 64-bit version of FNV-1a hash algorithm.
    /// </summary>
    static long IFNV1aParameters<long>.Prime => 1099511628211;

    /// <summary>
    /// Gets offset basis for 128-bit version of FNV-1a hash algorithm.
    /// </summary>
    static Int128 IFNV1aParameters<Int128>.Offset { get; } = Int128.Parse("144066263297769815596495629667062367629");

    /// <summary>
    /// Gets prime number for 128-bit version of FNV-1a hash algorithm.
    /// </summary>
    static Int128 IFNV1aParameters<Int128>.Prime { get; } = Int128.Parse("309485009821345068724781371");
}