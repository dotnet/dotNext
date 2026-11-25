using System.Text.Encodings.Web;

namespace DotNext.Text.Encodings.Web;

public sealed class FileUriTests : Test
{
    public static TheoryData<string, string> Paths => new()
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
        { @"\\unc\path\", @"\\unc\path\" },
        { @"\\unc", @"\\unc" },
        { @"\\?\C:\device\path", @"C:\device\path" },
        { @"\\.\C:\device\path", @"C:\device\path" },
        { @"\\?\UNC\unc\path", @"\\unc\path" },
        { @"\\.\unc\unc\path", @"\\unc\path" },
        { @"C:/forward/slashes", @"C:\forward\slashes" },
        { @"C:\", @"C:\" },

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
    [MemberData(nameof(Paths))]
    public static void FromFileNameToUri(string fileName, string expected)
    {
        var uri = new Uri(FileUri.CreateFromFileName(fileName), UriKind.Absolute);
        True(uri.IsFile);
        Equal(expected, uri.LocalPath);
    }

    [Fact]
    public static void GetUriExtensionMethod()
    {
        var fileName = OperatingSystem.IsWindows() ? "C:\\some\\path" : "/some/path";
        var uri = new FileInfo(fileName).Uri;
        Equal(fileName, uri.LocalPath);
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public static void TryCreateFromFileName(string fileName, string expected)
    {
        Span<char> buffer = stackalloc char[512];
        True(FileUri.TryCreateFromFileName(fileName, UrlEncoder.Default, buffer, out var charsWritten));

        var uri = new Uri(buffer.Slice(0, charsWritten).ToString(), UriKind.Absolute);
        Equal(expected, uri.LocalPath);
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public static void TryConvertToUri(string fileName, string expected)
    {
        True(Uri.TryCreateFromFileName(fileName, settings: null, out var uri));
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
    [InlineData("../path")]
    [InlineData("./path")]
    [InlineData("")]
    [InlineData("C:")]
    [InlineData("CD:\\path")]
    [InlineData(":\\path")]
    [InlineData("1:\\path")]
    [InlineData("x#y:\\path")]
    [InlineData("\\\\")]
    [InlineData("\\\\\\path")]
    [InlineData("\\\\my host\\share")]
    [InlineData("\\\\host#name\\share")]
    [InlineData("\\\\.\\pipe\\name")]
    [InlineData("\\\\?\\Volume{b75e2c83-0000-0000-0000-602f00000000}\\path")]
    [InlineData("\\\\?\\UNC\\")]
    public static void CheckFullyQualifiedPath(string path)
    {
        Throws<ArgumentException>(() => FileUri.CreateFromFileName(path));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/a/")]
    [InlineData("/a b/c")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\a\")]
    [InlineData(@"\\?\C:\a")]
    [InlineData(@"\\host")]
    [InlineData(@"\\host\share\")]
    [InlineData(@"\\?\UNC\host\share")]
    public static void TryCreateFromFileNameSmallBuffer(string fileName)
    {
        var expected = FileUri.CreateFromFileName(fileName);
        True(FileUri.GetMaxEncodedLength(fileName) >= expected.Length);

        var buffer = new char[expected.Length];
        for (var size = 0; size < expected.Length; size++)
            False(FileUri.TryCreateFromFileName(fileName, UrlEncoder.Default, buffer.AsSpan(0, size), out _));

        True(FileUri.TryCreateFromFileName(fileName, UrlEncoder.Default, buffer, out var charsWritten));
        Equal(expected, buffer.AsSpan(0, charsWritten).ToString());
    }
}
