using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext;

using static Runtime.Intrinsics;
using FNV1a32 = IO.Hashing.FNV1a32;

/// <summary>
/// Represents bitwise comparer for the arbitrary value type.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class BitwiseComparer<T> : IEqualityComparer<T>, IComparer<T>
    where T : unmanaged
{
    private BitwiseComparer()
    {
    }

    /// <summary>
    /// Gets instance of this comparer.
    /// </summary>
    /// <remarks>
    /// Use this property only if you need object implementing <see cref="IEqualityComparer{T}"/>
    /// or <see cref="IComparer{T}"/> interface. Otherwise, use static methods.
    /// </remarks>
    /// <returns>The instance of this comparer.</returns>
    public static BitwiseComparer<T> Instance { get; } = new();

    /// <summary>
    /// Checks bitwise equality between two values of different value types.
    /// </summary>
    /// <remarks>
    /// This method doesn't use <see cref="object.Equals(object)"/>
    /// even if it is overridden by value type.
    /// </remarks>
    /// <typeparam name="TOther">Type of second value.</typeparam>
    /// <param name="first">The first value to check.</param>
    /// <param name="second">The second value to check.</param>
    /// <returns><see langword="true"/>, if both values are equal; otherwise, <see langword="false"/>.</returns>
    public static unsafe bool Equals<TOther>(in T first, in TOther second)
        where TOther : unmanaged
        => sizeof(T) == sizeof(TOther) && sizeof(T) switch
        {
            0 => true,
            sizeof(byte) => InToRef<T, byte>(in first) == InToRef<TOther, byte>(in second),
            sizeof(ushort) => ReadUnaligned<ushort>(ref InToRef<T, byte>(in first)) == ReadUnaligned<ushort>(ref InToRef<TOther, byte>(in second)),
            sizeof(uint) => ReadUnaligned<uint>(ref InToRef<T, byte>(in first)) == ReadUnaligned<uint>(ref InToRef<TOther, byte>(in second)),
            sizeof(ulong) => ReadUnaligned<ulong>(ref InToRef<T, byte>(in first)) == ReadUnaligned<ulong>(ref InToRef<TOther, byte>(in second)),
            _ => EqualsUnaligned(ref InToRef<T, byte>(in first), ref InToRef<TOther, byte>(in second), (nuint)SizeOf<T>()),
        };

    /// <summary>
    /// Compares bits of two values of the different type.
    /// </summary>
    /// <typeparam name="TOther">Type of the second value.</typeparam>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public static unsafe int Compare<TOther>(in T first, in TOther second)
        where TOther : unmanaged
    {
        var result = sizeof(T);
        result = result.CompareTo(sizeof(TOther));

        if (result is 0)
        {
            result = sizeof(T) switch
            {
                0 => 0,
                sizeof(byte) => InToRef<T, byte>(in first).CompareTo(InToRef<TOther, byte>(in second)),
                sizeof(ushort) => ReadUnaligned<ushort>(ref InToRef<T, byte>(in first)).CompareTo(ReadUnaligned<short>(ref InToRef<TOther, byte>(in second))),
                sizeof(uint) => ReadUnaligned<uint>(ref InToRef<T, byte>(in first)).CompareTo(ReadUnaligned<uint>(ref InToRef<TOther, byte>(in second))),
                sizeof(ulong) => ReadUnaligned<ulong>(ref InToRef<T, byte>(in first)).CompareTo(ReadUnaligned<ulong>(ref InToRef<TOther, byte>(in second))),
                _ => CompareUnaligned(ref InToRef<T, byte>(in first), ref InToRef<TOther, byte>(in second), (nuint)SizeOf<T>()),
            };
        }

        return result;
    }

    /// <summary>
    /// Computes hash code for the structure content.
    /// </summary>
    /// <param name="value">Value to be hashed.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>Content hash code.</returns>
    public static unsafe int GetHashCode(in T value, bool salted = true)
    {
        int hash;
        switch (sizeof(T))
        {
            default:
                return FNV1a32.Hash(Span.AsReadOnlyBytes(in value), salted);
            case 0:
                hash = 0;
                break;
            case sizeof(byte):
                hash = InToRef<T, byte>(in value);
                break;
            case sizeof(ushort):
                hash = ReadUnaligned<ushort>(ref InToRef<T, byte>(in value));
                break;
            case sizeof(uint):
                hash = ReadUnaligned<int>(ref InToRef<T, byte>(in value));
                break;
            case sizeof(ulong):
                hash = ReadUnaligned<ulong>(ref InToRef<T, byte>(in value)).GetHashCode();
                break;
        }

        if (salted)
            hash ^= RandomExtensions.BitwiseHashSalt;

        return hash;
    }

    /// <inheritdoc/>
    bool IEqualityComparer<T>.Equals(T x, T y) => Equals(in x, in y);

    /// <inheritdoc/>
    int IEqualityComparer<T>.GetHashCode(T obj) => GetHashCode(in obj);

    /// <inheritdoc/>
    int IComparer<T>.Compare(T x, T y) => Compare(in x, in y);
}