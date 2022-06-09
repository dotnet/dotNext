using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DotNext.Buffers;

[ExcludeFromCodeCoverage]
public sealed class MemoryTemplateTests : Test
{
    private static void Rewrite(int index, ArrayBufferWriter<char> writer)
    {
        switch (index)
        {
            case 0:
                writer.Write("world");
                break;
            default:
                writer.Write('!');
                break;
        }
    }

    [Fact]
    public static void RenderToBuffer()
    {
        const string placeholder = "%s";
        var template = "Hello, %s!%s".AsTemplate(placeholder);
        var writer = new ArrayBufferWriter<char>();
        template.Render(writer, Rewrite);
        Equal("Hello, world!!", writer.BuildString());

        writer.Clear();
        template = "%s%s".AsTemplate(placeholder);
        template.Render(writer, Rewrite);
        Equal("world!", writer.BuildString());

        writer.Clear();
        template = "%s!!%s".AsTemplate(placeholder);
        template.Render(writer, Rewrite);
        Equal("world!!!", writer.BuildString());
    }

    [Fact]
    public static void RenderToStringBuilder()
    {
        const string placeholder = "%s";
        var template = "Hello, %s!%s".AsTemplate(placeholder);
        var writer = new StringBuilder();
        string[] replacement = { "world", "!" };
        template.Render(writer, replacement);
        Equal("Hello, world!!", writer.ToString());

        writer.Clear();
        template = "%s%s".AsTemplate(placeholder);
        template.Render(writer, replacement);
        Equal("world!", writer.ToString());

        writer.Clear();
        template = "%s!!%s".AsTemplate(placeholder);
        template.Render(writer, replacement);
        Equal("world!!!", writer.ToString());
    }

    [Fact]
    public static void RenderToStringWriter()
    {
        const string placeholder = "%s";
        var template = "Hello, %s!%s".AsTemplate(placeholder);
        var writer = new StringWriter();
        string[] replacement = { "world", "!" };
        template.Render(writer, replacement);
        Equal("Hello, world!!", writer.ToString());

        writer.GetStringBuilder().Clear();
        template = "%s%s".AsTemplate(placeholder);
        template.Render(writer, replacement);
        Equal("world!", writer.ToString());

        writer.GetStringBuilder().Clear();
        template = "%s!!%s".AsTemplate(placeholder);
        template.Render(writer, replacement);
        Equal("world!!!", writer.ToString());
    }

    [Fact]
    public static void RenderToString()
    {
        const char placeholder = '%';
        var template = "Hello, %!%".AsTemplate(placeholder);
        string[] replacement = { "world", "!" };
        Equal("Hello, world!!", template.Render(replacement));

        template = "%%".AsTemplate(placeholder);
        Equal("world!", template.Render(replacement));

        template = "%!!%".AsTemplate(placeholder);
        Equal("world!!!", template.Render(replacement));
    }

    [Fact]
    public static void EmptyPlaceholder()
    {
        var template = new MemoryTemplate<char>("Hello, world!".AsMemory(), default);
        var writer = new ArrayBufferWriter<char>();
        template.Render(writer, Rewrite);
        Equal("Hello, world!", writer.BuildString());
    }

    [Fact]
    public static void LargePlaceholder()
    {
        var template = "Hello, world!".AsTemplate("{Very large placeholder}");
        var writer = new ArrayBufferWriter<char>();
        template.Render(writer, Rewrite);
        Equal("Hello, world!", writer.BuildString());
    }
}