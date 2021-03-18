using System;
using System.IO;
using Xunit;

namespace DotNext.IO.Log
{
    public sealed class ContentLocationExtensionTests : Test
    {
        [Fact]
        public static void FileNameEscape()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var extension = new ContentLocationExtension(new FileInfo(path));
            Equal(path, extension.Value.LocalPath, StringComparer.Ordinal);
        }
    }
}