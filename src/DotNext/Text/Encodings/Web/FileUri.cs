using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;

namespace DotNext.Text.Encodings.Web;

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
    private static readonly SearchValues<char> UnixDirectorySeparators = SearchValues.Create(UriPathSeparator);
    private static readonly SearchValues<char> WindowsDirectorySeparators = SearchValues.Create(UriPathSeparator, '\\');

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

        var result = new StringConverter();
        TryCreateFromFileNameCore(fileName, settings is null ? UrlEncoder.Default : UrlEncoder.Create(settings), ref result);
        return result.Result;
    }

    private static bool TryCreateFromFileNameCore<TConverter>(ReadOnlySpan<char> fileName, UrlEncoder encoder, ref TConverter converter)
        where TConverter : struct, IConverter, allows ref struct
    {
        var maxLength = GetMaxEncodedLengthCore(fileName, encoder);
        using var buffer = (uint)maxLength <= (uint)SpanOwner<char>.StackallocThreshold
            ? stackalloc char[maxLength]
            : new SpanOwner<char>(maxLength);

        return TryCreateFromFileNameCore(fileName, encoder, buffer.Span, out var writtenCount)
               && converter.Invoke(buffer.Span.Slice(0, writtenCount));
    }

    /// <summary>
    /// Gets the maximum number of characters that can be produced by <see cref="TryCreateFromFileName(ReadOnlySpan{char},UrlEncoder?,Span{char},out int)"/> method.
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
        writer += FileScheme;

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

                writer += UriPathSeparator;
                writer += drive;
                writer += endsWithTrailingSeparator ? [escapedDriveSeparatorChar, UriPathSeparator] : [escapedDriveSeparatorChar];
                break;
        }

        for (;; writer += UriPathSeparator)
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
    /// Extends <see cref="FileSystemInfo"/> type.
    /// </summary>
    /// <param name="file">The information about file system object.</param>
    extension(FileSystemInfo file)
    {
        /// <summary>
        /// Gets URI that points to the file system object.
        /// </summary>
        public Uri Uri
        {
            get
            {
                var converter = new StringConverter();
                TryCreateFromFileNameCore(file.FullName, UrlEncoder.Default, ref converter);
                return new(converter.Result, UriKind.Absolute);
            }
        }
    }

    /// <summary>
    /// Extends <see cref="Uri"/> data type.
    /// </summary>
    extension(Uri)
    {
        /// <summary>
        /// Tries to convert the file name to <see cref="Uri"/>.
        /// </summary>
        /// <param name="fileName">The fully-qualified file name.</param>
        /// <param name="settings">The encoding settings.</param>
        /// <param name="fileUri">Absolute file URI.</param>
        /// <returns><see langword="true"/> if <paramref name="fileName"/> is encoded successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="fileName"/> is not fully-qualified.</exception>
        public static bool TryCreateFromFileName(ReadOnlySpan<char> fileName, TextEncoderSettings? settings, [NotNullWhen(true)] out Uri? fileUri)
        {
            if (fileName.IsEmpty)
                throw new ArgumentException(ExceptionMessages.FullyQualifiedPathExpected, nameof(fileName));

            Unsafe.SkipInit(out fileUri);
            var converter = new UriConverter(ref fileUri);
            return TryCreateFromFileNameCore(fileName, settings is null ? UrlEncoder.Default : UrlEncoder.Create(settings), ref converter);
        }
    }

    private interface IConverter
    {
        bool Invoke(scoped ReadOnlySpan<char> value);
    }

    [StructLayout(LayoutKind.Auto)]
    private struct StringConverter() : IConverter
    {
        private string result = string.Empty;

        public readonly string Result => result;
        
        bool IConverter.Invoke(scoped ReadOnlySpan<char> value)
        {
            result = new(value);
            return true;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct UriConverter(ref Uri? result) : IConverter
    {
        private readonly ref Uri? result = ref result;

        bool IConverter.Invoke(scoped ReadOnlySpan<char> value) => Uri.TryCreate(new string(value), UriKind.Absolute, out result);
    }
}