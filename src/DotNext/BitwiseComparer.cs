using System.IO.Hashing;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext;

using Patterns;
using Runtime.InteropServices;
using static Runtime.CompilerServices.AdvancedHelpers;

/// <summary>
/// Represents bitwise comparer for the arbitrary value type.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class BitwiseComparer<T> :
    IEqualityComparer<T>,
    IComparer<T>,
    ISingleton<BitwiseComparer<T>>,
    IAlternateEqualityComparer<ReadOnlySpan<byte>, T>,
    IAlternateEqualityComparer<ReadOnlyMemory<byte>, T>
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
    public static bool Equals<TOther>(in T first, in TOther second)
        where TOther : unmanaged, allows ref struct
        => MemoryMarshal.AsReadOnlyBytes(in first).SequenceEqual(MemoryMarshal.AsReadOnlyBytes(in second));

    /// <summary>
    /// Compares bits of two values of the different type.
    /// </summary>
    /// <typeparam name="TOther">Type of the second value.</typeparam>
    /// <param name="first">The first value to compare.</param>
    /// <param name="second">The second value to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public static int Compare<TOther>(in T first, in TOther second)
        where TOther : unmanaged, allows ref struct
        => MemoryMarshal.AsReadOnlyBytes(in first).SequenceCompareTo(MemoryMarshal.AsReadOnlyBytes(in second));

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
                return (int)XxHash32.HashToUInt32(MemoryMarshal.AsReadOnlyBytes(in value), salted ? RandomExtensions.BitwiseHashSalt : 0);
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

    /// <inheritdoc/>
    bool IAlternateEqualityComparer<ReadOnlySpan<byte>, T>.Equals(ReadOnlySpan<byte> alternate, T other)
        => Equals(alternate, in other);
    
    private static bool Equals(ReadOnlySpan<byte> alternate, ref readonly T other)
        => alternate.SequenceEqual(MemoryMarshal.AsReadOnlyBytes(in other));

    /// <inheritdoc/>
    int IAlternateEqualityComparer<ReadOnlySpan<byte>, T>.GetHashCode(ReadOnlySpan<byte> alternate)
        => GetHashCode(alternate);

    private static int GetHashCode(ReadOnlySpan<byte> alternate)
        => (int)XxHash32.HashToUInt32(alternate, RandomExtensions.BitwiseHashSalt);

    /// <inheritdoc/>
    T IAlternateEqualityComparer<ReadOnlySpan<byte>, T>.Create(ReadOnlySpan<byte> alternate)
        => Create(alternate);

    private static T Create(ReadOnlySpan<byte> alternate) => MemoryMarshal.AsRef<T>(alternate);

    /// <inheritdoc/>
    bool IAlternateEqualityComparer<ReadOnlyMemory<byte>, T>.Equals(ReadOnlyMemory<byte> alternate, T other)
        => Equals(alternate.Span, in other);

    /// <inheritdoc/>
    int IAlternateEqualityComparer<ReadOnlyMemory<byte>, T>.GetHashCode(ReadOnlyMemory<byte> alternate)
        => GetHashCode(alternate.Span);

    /// <inheritdoc/>
    T IAlternateEqualityComparer<ReadOnlyMemory<byte>, T>.Create(ReadOnlyMemory<byte> alternate)
        => Create(alternate.Span);
}