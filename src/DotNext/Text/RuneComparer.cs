using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Text;

/// <summary>
/// Represents <see cref="Rune"/> comparison operation that uses specific case and culture-based
/// or ordinal comparison rules.
/// </summary>
public abstract class RuneComparer : IEqualityComparer<Rune>, IComparer<Rune>
{
    private static readonly RuneComparer?[] CachedComparers = new RuneComparer?[(int)Enum.GetValues<StringComparison>().Max() + 1];
    
    /// <summary>
    /// Initializes a new instance of comparer.
    /// </summary>
    protected RuneComparer()
    {
    }
    
    /// <summary>
    /// Determines whether the two runes are equal.
    /// </summary>
    /// <param name="x">The first rune to compare.</param>
    /// <param name="y">The second rune to compare.</param>
    /// <returns><see langword="true"/> if both runes are equal; otherwise, <see langword="false"/>.</returns>
    public abstract bool Equals(Rune x, Rune y);

    /// <summary>
    /// Determines whether the two runes are equal.
    /// </summary>
    /// <param name="x">The first rune to compare.</param>
    /// <param name="y">The second rune to compare.</param>
    /// <param name="comparisonType">The comparison type.</param>
    /// <returns><see langword="true"/> if both runes are equal; otherwise, <see langword="false"/>.</returns>
    public static bool Equals(Rune x, Rune y, StringComparison comparisonType)
        => new RuneBuffer(x).Equals(new RuneBuffer(y), comparisonType);

    /// <summary>
    /// Compares two runes and returns an indication of their relative sort order.
    /// </summary>
    /// <param name="x">The first rune to compare.</param>
    /// <param name="y">The second rune to compare.</param>
    /// <returns>A number indicating relative sort order of the runes.</returns>
    public abstract int Compare(Rune x, Rune y);

    /// <summary>
    /// Compares two runes and returns an indication of their relative sort order.
    /// </summary>
    /// <param name="x">The first rune to compare.</param>
    /// <param name="y">The second rune to compare.</param>
    /// <param name="comparisonType">The comparison type.</param>
    /// <returns>A number indicating relative sort order of the runes.</returns>
    public static int Compare(Rune x, Rune y, StringComparison comparisonType)
        => new RuneBuffer(x).Compare(new RuneBuffer(y), comparisonType);
    
    /// <summary>
    /// Gets the hash code for the specified rune.
    /// </summary>
    /// <param name="rune">A rune.</param>
    /// <returns>A hash code of the rune.</returns>
    public abstract int GetHashCode(Rune rune);

    /// <summary>
    /// Gets the hash code for the specified rune.
    /// </summary>
    /// <param name="rune">A rune.</param>
    /// <param name="comparisonType">The comparison type.</param>
    /// <returns>A hash code of the rune.</returns>
    public static int GetHashCode(Rune rune, StringComparison comparisonType)
        => new RuneBuffer(rune).GetHashCode(comparisonType);
    
    /// <summary>
    /// Converts <see cref="StringComparison"/> to <see cref="RuneComparer"/>.
    /// </summary>
    /// <param name="comparison">A rune comparer instance to convert.</param>
    /// <returns>A comparer representing the specified comparison type.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="comparison"/> is invalid.</exception>
    public static RuneComparer FromComparison(StringComparison comparison)
    {
        var index = (int)comparison;

        return (uint)index < (uint)CachedComparers.Length
            ? EnsureInitialized(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(CachedComparers), index), comparison)
            : throw new ArgumentOutOfRangeException(nameof(comparison));

        static RuneComparer EnsureInitialized(ref RuneComparer? comparer, StringComparison comparison)
        {
            DefaultRuneComparer newComparer;
            return comparer ?? Interlocked.CompareExchange(ref comparer, newComparer = new(comparison), null) ?? newComparer;
        }
    }
    
    /// <summary>
    /// Creates rune comparer for the specified culture.
    /// </summary>
    /// <param name="culture">A culture whose linguistic rules are used to perform a string comparison.</param>
    /// <param name="options">Comparison options.</param>
    /// <returns>Culture-specific comparer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    public static RuneComparer Create(CultureInfo culture, CompareOptions options)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return new CultureSpecificRuneComparer(culture, options);
    }
}

file sealed class DefaultRuneComparer(StringComparison comparison) : RuneComparer
{
    public override bool Equals(Rune x, Rune y) => Equals(x, y, comparison);

    public override int GetHashCode(Rune rune) => GetHashCode(rune, comparison);

    public override int Compare(Rune x, Rune y) => Compare(x, y, comparison);

    public override string ToString() => comparison.ToString();
}

file sealed class CultureSpecificRuneComparer(CultureInfo culture, CompareOptions options) : RuneComparer
{
    private readonly CompareInfo comparison = culture.CompareInfo;

    public override bool Equals(Rune x, Rune y)
        => Compare(x, y) is 0;

    public override int Compare(Rune x, Rune y)
        => new RuneBuffer(x).Compare(new RuneBuffer(y), comparison, options);

    public override int GetHashCode(Rune rune)
        => new RuneBuffer(rune).GetHashCode(comparison, options);
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct RuneBuffer
{
    private readonly InlineArray2<char> buffer;
    private readonly int length;

    public RuneBuffer(Rune rune)
    {
        Unsafe.SkipInit(out buffer);
        var encoded = rune.TryEncodeToUtf16(buffer, out length);
        Debug.Assert(encoded);
    }

    [UnscopedRef]
    private ReadOnlySpan<char> Buffer
    {
        get
        {
            ReadOnlySpan<char> result = buffer;
            return result.Slice(0, length);
        }
    }

    public bool Equals(in RuneBuffer other, StringComparison comparison)
        => Buffer.Equals(other.Buffer, comparison);

    public int Compare(in RuneBuffer other, StringComparison comparison)
        => Buffer.CompareTo(other.Buffer, comparison);

    public int Compare(in RuneBuffer other, CompareInfo comparison, CompareOptions options)
        => comparison.Compare(Buffer, other.Buffer, options);

    public int GetHashCode(StringComparison comparison) => string.GetHashCode(Buffer, comparison);

    public int GetHashCode(CompareInfo comparison, CompareOptions options)
        => comparison.GetHashCode(Buffer, options);
}