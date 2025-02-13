using System.Numerics;

namespace DotNext.IO.Hashing;

/// <summary>
/// Represents parameters of FNV-1a hash algorithm.
/// </summary>
/// <typeparam name="THash">The type representing a hash.</typeparam>
public interface IFNV1aParameters<out THash>
    where THash : unmanaged, IBinaryNumber<THash>
{
    /// <summary>
    /// Gets offset basis.
    /// </summary>
    static abstract THash Offset { get; }

    /// <summary>
    /// Gets prime number.
    /// </summary>
    static abstract THash Prime { get; }
}