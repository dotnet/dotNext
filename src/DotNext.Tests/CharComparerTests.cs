using System.Globalization;

namespace DotNext;

public sealed class CharComparerTests : Test
{
    [Fact]
    public static void CompareUsingStringComparison()
    {
        Equal('a', 'A', CharComparer.FromComparison(StringComparison.OrdinalIgnoreCase));
        NotEqual('a', 'A', CharComparer.FromComparison(StringComparison.Ordinal));

        Equal('a', 'A', CharComparer.FromComparison(StringComparison.InvariantCultureIgnoreCase));
        NotEqual('a', 'A', CharComparer.FromComparison(StringComparison.InvariantCulture));
    }

    [Fact]
    public static void EqualityOfComparers()
    {
        Same(CharComparer.FromComparison(StringComparison.Ordinal), CharComparer.FromComparison(StringComparison.Ordinal));
        True(CharComparer.FromComparison(StringComparison.Ordinal).Equals(CharComparer.FromComparison(StringComparison.Ordinal)));
        Equal(CharComparer.FromComparison(StringComparison.Ordinal).GetHashCode(), CharComparer.FromComparison(StringComparison.Ordinal).GetHashCode());

        Equal(CharComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase), CharComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase));
        Equal(CharComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase).GetHashCode(), CharComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase).GetHashCode());
    }

    [Fact]
    public static void ToStringFromComparison()
    {
        Equal(StringComparison.Ordinal.ToString(), CharComparer.FromComparison(StringComparison.Ordinal).ToString());
    }

    [Fact]
    public static void CompareUsingCustomCulture()
    {
        Equal('a', 'A', CharComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase));
    }
}