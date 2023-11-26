using System.Globalization;

namespace DotNext;

/// <summary>
/// Represents a character comparison operation that uses specific case and culture-based
/// or ordinal comparison rules.
/// </summary>
/// <seealso cref="StringComparer"/>
public abstract class CharComparer : IEqualityComparer<char>, IComparer<char>
{
    private static readonly DefaultCharComparer?[] CachedComparers = new DefaultCharComparer?[(int)Enum.GetValues<StringComparison>().Max() + 1];

    /// <summary>
    /// Initializes a new instance of comparer.
    /// </summary>
    protected CharComparer()
    {
    }

    /// <summary>
    /// Determines whether the two characters are equal.
    /// </summary>
    /// <param name="x">The first character to compare.</param>
    /// <param name="y">The second character to compare.</param>
    /// <returns><see langword="true"/> if both characters are equal; otherwise, <see langword="false"/>.</returns>
    public abstract bool Equals(char x, char y);

    /// <summary>
    /// Determines whether the two characters are equal.
    /// </summary>
    /// <param name="x">The first character to compare.</param>
    /// <param name="y">The second character to compare.</param>
    /// <param name="comparisonType">The comparison type.</param>
    /// <returns><see langword="true"/> if both characters are equal; otherwise, <see langword="false"/>.</returns>
    public static bool Equals(char x, char y, StringComparison comparisonType)
        => MemoryExtensions.Equals(new(ref x), new(ref y), comparisonType);

    /// <summary>
    /// Compares two characters and returns an indication of their relative sort order.
    /// </summary>
    /// <param name="x">The first character to compare.</param>
    /// <param name="y">The second character to compare.</param>
    /// <returns>A number indicating relative sort order of the characters.</returns>
    public abstract int Compare(char x, char y);

    /// <summary>
    /// Compares two characters and returns an indication of their relative sort order.
    /// </summary>
    /// <param name="x">The first character to compare.</param>
    /// <param name="y">The second character to compare.</param>
    /// <param name="comparisonType">The comparison type.</param>
    /// <returns>A number indicating relative sort order of the characters.</returns>
    public static int Compare(char x, char y, StringComparison comparisonType)
        => MemoryExtensions.CompareTo(new(ref x), new(ref y), comparisonType);

    /// <summary>
    /// Gets the hash code for the specified character.
    /// </summary>
    /// <param name="ch">A character.</param>
    /// <returns>A hash code of the character.</returns>
    public abstract int GetHashCode(char ch);

    /// <summary>
    /// Gets the hash code for the specified character.
    /// </summary>
    /// <param name="ch">A character.</param>
    /// <param name="comparisonType">The comparison type.</param>
    /// <returns>A hash code of the character.</returns>
    public static int GetHashCode(char ch, StringComparison comparisonType)
        => string.GetHashCode(new(ref ch), comparisonType);

    /// <summary>
    /// Converts <see cref="StringComparison"/> to <see cref="CharComparer"/>.
    /// </summary>
    /// <param name="comparison">A character comparer instance to convert.</param>
    /// <returns>A comparer representing the specified comparison type.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="comparison"/> is invalid.</exception>
    public static CharComparer FromComparison(StringComparison comparison)
    {
        var index = (int)comparison;

        return (uint)index < (uint)CachedComparers.Length ?
            EnsureInitialized(ref CachedComparers[index], comparison)
            : throw new ArgumentOutOfRangeException(nameof(comparison));

        static DefaultCharComparer EnsureInitialized(ref DefaultCharComparer? comparer, StringComparison comparison)
        {
            DefaultCharComparer newComparer;
            return Volatile.Read(ref comparer) ?? Interlocked.CompareExchange(ref comparer, newComparer = new(comparison), null) ?? newComparer;
        }
    }

    /// <summary>
    /// Creates character comparer for the specified culture.
    /// </summary>
    /// <param name="culture">A culture whose linguistic rules are used to perform a string comparison.</param>
    /// <param name="options">Comparison options.</param>
    /// <returns>Culture-specific comparer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    public static CharComparer Create(CultureInfo culture, CompareOptions options)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return new CultureSpecificCharComparer(culture, options);
    }

    private sealed class DefaultCharComparer(StringComparison comparisonType) : CharComparer
    {
        public override bool Equals(char x, char y)
            => Equals(x, y, comparisonType);

        public override int Compare(char x, char y)
            => Compare(x, y, comparisonType);

        public override int GetHashCode(char ch)
            => GetHashCode(ch, comparisonType);

        public override string ToString() => comparisonType.ToString();
    }

    private sealed class CultureSpecificCharComparer(CultureInfo culture, CompareOptions options) : CharComparer
    {
        private readonly CompareInfo comparison = culture.CompareInfo;

        public override bool Equals(char x, char y)
            => Compare(x, y) is 0;

        public override int Compare(char x, char y)
            => comparison.Compare(new ReadOnlySpan<char>(ref x), new(ref y), options);

        public override int GetHashCode(char ch)
            => comparison.GetHashCode(new ReadOnlySpan<char>(ref ch), options);
    }
}