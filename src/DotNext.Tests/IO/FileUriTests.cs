using System.Text.Encodings.Web;

namespace DotNext.IO;

public sealed class FileUriTests : Test
{
    public static TheoryData<string, string> GetPaths() => new()
    {
        // Windows path
        { @"C:\without\whitespace", @"C:\without\whitespace" },
        { @"C:\with whitespace", @"C:\with whitespace" },
        { @"C:\with\trailing\backslash", @"C:\with\trailing\backslash" },
        { @"C:\with\trailing\backslash and space", @"C:\with\trailing\backslash and space" },
        { @"C:\with\..\relative\.\components\", @"C:\relative\components\" },
        { @"C:\with\specials\chars\#\$\", @"C:\with\specials\chars\#\$\" },
        { @"C:\с\кириллицей", @"C:\с\кириллицей" },
        { @"C:\ελληνικά\γράμματα", @"C:\ελληνικά\γράμματα" },
        { @"\\unc\path", @"\\unc\path" },

        // Unix path
        { "/without/whitespace", "/without/whitespace" },
        { "/with whitespace", "/with whitespace" },
        { "/with/trailing/slash/", "/with/trailing/slash/" },
        { "/with/trailing/slash and space/", "/with/trailing/slash and space/" },
        { "/with/../relative/./components/", "/relative/components/" },
        { "/with/special/chars/?/>/</", "/with/special/chars/?/>/</" },
        { "/с/кириллицей", "/с/кириллицей" },
        { "/ελληνικά/γράμματα", "/ελληνικά/γράμματα" }
    };

    [Theory]
    [MemberData(nameof(GetPaths))]
    public static void EncodeAsUri(string fileName, string expected)
    {
        var uri = new Uri(FileUri.Encode(fileName), UriKind.Absolute);
        True(uri.IsFile);
        Equal(expected, uri.LocalPath);
    }

    [Theory]
    [MemberData(nameof(GetPaths))]
    public static void EncodeAsUriChars(string fileName, string expected)
    {
        Span<char> buffer = stackalloc char[512];
        True(FileUri.TryEncode(fileName, UrlEncoder.Default, buffer, out var charsWritten));

        var uri = new Uri(buffer.Slice(0, charsWritten).ToString(), UriKind.Absolute);
        Equal(expected, uri.LocalPath);
    }
    
    [Fact]
    public static void MaxEncodedLength()
    {
        const string path = "/some/path";
        True(FileUri.GetMaxEncodedLength(path) > path.Length);
    }

    [Theory]
    [InlineData("~/path/name")]
    [InlineData("C:path\\name")]
    [InlineData("")]
    public static void CheckFullyQualifiedPath(string path)
    {
        Throws<ArgumentException>(() => FileUri.Encode(path));
    }
}