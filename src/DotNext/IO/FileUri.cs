using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents operations to work with <c>file://</c> scheme.
/// </summary>
public static class FileUri
{
    // On Windows:
    // C:\folder => file:///C|/folder
    // \\hostname\folder => file://hostname/folder
    // \\?\folder => file://?/folder
    // \\.\folder => file://./folder
    private const string FileScheme = "file://";
    private const char UriPathSeparator = '/';
    private static readonly SearchValues<char> UnixDirectorySeparators = SearchValues.Create([UriPathSeparator]);
    private static readonly SearchValues<char> WindowsDirectorySeparators = SearchValues.Create([UriPathSeparator, '\\']);

    /// <summary>
    /// Encodes file name as URI.
    /// </summary>
    /// <param name="fileName">The fully-qualified file name.</param>
    /// <param name="settings">The encoding settings.</param>
    /// <returns><paramref name="fileName"/> as URI. The return value can be passed to <see cref="Uri(string)"/> constructor.</returns>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is not fully-qualified.</exception>
    public static string CreateFromFileName(ReadOnlySpan<char> fileName, TextEncoderSettings? settings = null)
    {
        if (fileName.IsEmpty)
            throw new ArgumentException(ExceptionMessages.FullyQualifiedPathExpected, nameof(fileName));

        return CreateFromFileNameCore(fileName, settings is null ? UrlEncoder.Default : UrlEncoder.Create(settings));
    }

    private static string CreateFromFileNameCore(ReadOnlySpan<char> fileName, UrlEncoder encoder)
    {
        var maxLength = GetMaxEncodedLengthCore(fileName, encoder);
        using var buffer = (uint)maxLength <= (uint)SpanOwner<char>.StackallocThreshold
            ? stackalloc char[maxLength]
            : new SpanOwner<char>(maxLength);

        return TryCreateFromFileNameCore(fileName, encoder, buffer.Span, out var writtenCount)
            ? new(buffer.Span.Slice(0, writtenCount))
            : string.Empty;
    }

    /// <summary>
    /// Gets the maximum number of characters that can be produced by <see cref="TryCreateFromFileName"/> method.
    /// </summary>
    /// <param name="fileName">The file name to be encoded.</param>
    /// <param name="encoder">The encoder.</param>
    /// <returns>The maximum number of characters that can be produced by the encoder.</returns>
    public static int GetMaxEncodedLength(ReadOnlySpan<char> fileName, UrlEncoder? encoder = null)
        => fileName.IsEmpty ? 0 : GetMaxEncodedLengthCore(fileName, encoder ?? UrlEncoder.Default);

    private static int GetMaxEncodedLengthCore(ReadOnlySpan<char> fileName, UrlEncoder encoder)
        => FileScheme.Length
           + Unsafe.BitCast<bool, byte>(fileName[0] is not UriPathSeparator)
           + encoder.MaxOutputCharactersPerInputCharacter * fileName.Length;

    /// <summary>
    /// Tries to encode file name as URI.
    /// </summary>
    /// <param name="fileName">The fully-qualified file name.</param>
    /// <param name="encoder">The encoder that is used to encode the file name.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="charsWritten">The number of characters written to <paramref name="output"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="fileName"/> is encoded successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is not fully-qualified.</exception>
    public static bool TryCreateFromFileName(ReadOnlySpan<char> fileName, UrlEncoder? encoder, Span<char> output, out int charsWritten)
    {
        if (fileName.IsEmpty)
            throw new ArgumentException(ExceptionMessages.FullyQualifiedPathExpected, nameof(fileName));

        return TryCreateFromFileNameCore(fileName, encoder ?? UrlEncoder.Default, output, out charsWritten);
    }

    private static bool TryCreateFromFileNameCore(ReadOnlySpan<char> fileName, UrlEncoder encoder, Span<char> output, out int charsWritten)
    {
        var writer = new SpanWriter<char>(output);
        writer.Write(FileScheme);

        bool endsWithTrailingSeparator;
        SearchValues<char> directoryPathSeparators;
        switch (fileName)
        {
            case [UriPathSeparator, ..]: // Unix path
                directoryPathSeparators = UnixDirectorySeparators;
                break;
            case ['\\', '\\', .. var rest]: // Windows UNC path
                directoryPathSeparators = WindowsDirectorySeparators;
                fileName = rest;
                break;
            default: // Windows path
                const char driveSeparator = ':';
                const char escapedDriveSeparatorChar = '|';
                if (GetPathComponent(ref fileName, directoryPathSeparators = WindowsDirectorySeparators, out endsWithTrailingSeparator)
                    is not [.. var drive, driveSeparator])
                    throw new ArgumentException(ExceptionMessages.FullyQualifiedPathExpected, nameof(fileName));

                writer.Add(UriPathSeparator);
                writer.Write(drive);
                writer.Write(endsWithTrailingSeparator ? [escapedDriveSeparatorChar, UriPathSeparator] : [escapedDriveSeparatorChar]);
                break;
        }

        for (;; writer.Add(UriPathSeparator))
        {
            var component = GetPathComponent(ref fileName, directoryPathSeparators, out endsWithTrailingSeparator);
            if (encoder.Encode(component, writer.RemainingSpan, out _, out charsWritten) is not OperationStatus.Done)
                return false;

            writer.Advance(charsWritten);
            if (!endsWithTrailingSeparator)
                break;
        }

        charsWritten = writer.WrittenCount;
        return true;
    }

    private static ReadOnlySpan<char> GetPathComponent(ref ReadOnlySpan<char> fileName, SearchValues<char> directorySeparatorChars, out bool endsWithTrailingSeparator)
    {
        ReadOnlySpan<char> component;
        var index = fileName.IndexOfAny(directorySeparatorChars);
        if (endsWithTrailingSeparator = index >= 0)
        {
            component = fileName.Slice(0, index);
            fileName = fileName.Slice(index + 1);
        }
        else
        {
            component = fileName;
            fileName = default;
        }

        return component;
    }

    /// <summary>
    /// Gets URI that points to the file system object.
    /// </summary>
    /// <param name="fileInfo">The information about file system object.</param>
    /// <param name="settings">The encoding settings.</param>
    /// <returns><see cref="Uri"/> that points to the file system object.</returns>
    public static Uri GetUri(this FileSystemInfo fileInfo, TextEncoderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);

        return new(CreateFromFileNameCore(fileInfo.FullName, settings is null ? UrlEncoder.Default : UrlEncoder.Create(settings)), UriKind.Absolute);
    }
}