using System.Globalization;
using System.Text;

namespace DotNext.Text;

public sealed class RuneComparerTests : Test
{
    [Fact]
    public static void CompareUsingStringComparison()
    {
        Equal((Rune)'a', (Rune)'A', RuneComparer.FromComparison(StringComparison.OrdinalIgnoreCase));
        NotEqual((Rune)'a', (Rune)'A', RuneComparer.FromComparison(StringComparison.Ordinal));

        Equal((Rune)'a', (Rune)'A', RuneComparer.FromComparison(StringComparison.InvariantCultureIgnoreCase));
        NotEqual((Rune)'a', (Rune)'A', RuneComparer.FromComparison(StringComparison.InvariantCulture));
    }

    [Fact]
    public static void EqualityOfComparers()
    {
        Same(RuneComparer.FromComparison(StringComparison.Ordinal), RuneComparer.FromComparison(StringComparison.Ordinal));
        Equal(RuneComparer.FromComparison(StringComparison.Ordinal).GetHashCode(), RuneComparer.FromComparison(StringComparison.Ordinal).GetHashCode());
    }

    [Fact]
    public static void ToStringFromComparison()
    {
        Equal(nameof(StringComparison.Ordinal), RuneComparer.FromComparison(StringComparison.Ordinal).ToString());
    }

    [Fact]
    public static void CompareUsingCustomCulture()
    {
        Equal((Rune)'a', (Rune)'A', RuneComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase));
    }
}