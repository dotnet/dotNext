using System.Text;

namespace DotNext.Text;

public sealed class CompositeFormatExtensionsTests : Test
{
    [Fact]
    public static void WhenStringIsNotInterned()
    {
        ReadOnlySpan<byte> format = "Hello from non-interned format, {0}!"u8;
        var str = Encoding.UTF8.GetString(format);
        Null(CompositeFormat.IsInterned(str));
    }

    [Fact]
    public static void WhenStringIsInterned()
    {
        const string interned = "Hello from interned format, {0}!";
        ReadOnlySpan<byte> format = "Hello from interned format, {0}!"u8;
        var nonInterned = Encoding.UTF8.GetString(format);
        
        Equal(interned, nonInterned);
        Same(CompositeFormat.IsInterned(interned), CompositeFormat.IsInterned(nonInterned));
    }

    [Fact]
    public static void CreateFromConstant()
    {
        const string format = "Constant format {0} {1}";
        Equal(format, CompositeFormat.Of(format).Format);
    }
}