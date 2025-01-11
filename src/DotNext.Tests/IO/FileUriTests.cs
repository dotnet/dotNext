using System.Text.Encodings.Web;

namespace DotNext.IO;

public sealed class FileUriTests : Test
{
    public static TheoryData<string, string> GetPaths()
    {
        var data = new TheoryData<string, string>();
        if (OperatingSystem.IsWindows())
        {
            data.Add(@"C:\without\whitespace", @"C:\without\whitespace");
            data.Add(@"C:\with whitespace", @"C:\with whitespace");
            data.Add(@"C:\with\trailing\backslash", @"C:\with\trailing\backslash");
            data.Add(@"C:\with\trailing\backslash and space", @"C:\with\trailing\backslash and space");
            data.Add(@"C:\with\..\relative\.\components\", @"C:\relative\components\");
            data.Add(@"C:\with\specials\chars\#\$\", @"C:\with\specials\chars\#\$\");
            data.Add(@"C:\с\кириллицей", @"C:\с\кириллицей");
            data.Add(@"C:\ελληνικά\γράμματα", @"C:\ελληνικά\γράμματα");
            data.Add(@"\\unc\path", @"\\unc\path");
        }
        else
        {
            data.Add("/without/whitespace", "/without/whitespace");
            data.Add("/with whitespace", "/with whitespace");
            data.Add("/with/trailing/slash/", "/with/trailing/slash/");
            data.Add("/with/trailing/slash and space/", "/with/trailing/slash and space/");
            data.Add("/with/../relative/./components/", "/relative/components/");
            data.Add("/with/special/chars/?/>/</", "/with/special/chars/?/>/</");
            data.Add("/с/кириллицей", "/с/кириллицей");
            data.Add("/ελληνικά/γράμματα", "/ελληνικά/γράμματα");
        }

        return data;
    }

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
}