using System.Buffers;
using System.Diagnostics;
using System.Text.Encodings.Web;
using DotNext.Buffers;

namespace DotNext.IO;

/// <summary>
/// Represents operations to work with <c>file://</c> scheme.
/// </summary>
public static class FileUri
{
    private const string FileScheme = "file://";

    /// <summary>
    /// Encodes file name as URI.
    /// </summary>
    /// <param name="fileName">The fully-qualified file name.</param>
    /// <param name="settings">The encoding settings.</param>
    /// <returns><paramref name="fileName"/> as <see cref="Uri"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is not fully-qualified.</exception>
    public static Uri Encode(ReadOnlySpan<char> fileName, TextEncoderSettings? settings = null)
    {
        ThrowIfNotFullyQualified(fileName);
        var encoder = settings is null ? UrlEncoder.Default : UrlEncoder.Create(settings);
        var maxLength = FileScheme.Length + encoder.MaxOutputCharactersPerInputCharacter * fileName.Length;
        using var buffer = (uint)maxLength <= (uint)SpanOwner<char>.StackallocThreshold
            ? stackalloc char[maxLength]
            : new SpanOwner<char>(maxLength);

        TryEncodeCore(fileName, encoder, buffer.Span, out var writtenCount);
        return new(buffer.Span.Slice(0, writtenCount).ToString(), UriKind.Absolute);
    }

    /// <summary>
    /// Tries to encode file name as URI.
    /// </summary>
    /// <param name="fileName">The fully-qualified file name.</param>
    /// <param name="encoder">The encoder that is used to encode the file name.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="charsWritten">The number of characters written to <paramref name="output"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="fileName"/> is encoded successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is not fully-qualified.</exception>
    public static bool TryEncode(ReadOnlySpan<char> fileName, UrlEncoder? encoder, Span<char> output, out int charsWritten)
    {
        ThrowIfNotFullyQualified(fileName);

        return TryEncodeCore(fileName, encoder ?? UrlEncoder.Default, output, out charsWritten);
    }

    [StackTraceHidden]
    private static void ThrowIfNotFullyQualified(ReadOnlySpan<char> fileName)
    {
        if (!Path.IsPathFullyQualified(fileName))
            throw new ArgumentException(ExceptionMessages.FullyQualifiedPathExpected, nameof(fileName));
    }

    private static bool TryEncodeCore(ReadOnlySpan<char> fileName, UrlEncoder encoder, Span<char> output, out int charsWritten)
    {
        var result = false;
        var writer = new SpanWriter<char>(output);
        writer.Write(FileScheme);
        while (!fileName.IsEmpty)
        {
            var index = fileName.IndexOf(Path.DirectorySeparatorChar);
            ReadOnlySpan<char> component;
            if (index >= 0)
            {
                component = fileName.Slice(0, index);
                fileName = fileName.Slice(index + 1);
            }
            else
            {
                component = fileName;
                fileName = default;
            }

            result = encoder.Encode(component, writer.RemainingSpan, out _, out charsWritten) is OperationStatus.Done;
            if (!result)
                break;

            writer.Advance(charsWritten);
            if (index >= 0)
                writer.Add(Path.DirectorySeparatorChar);
        }

        charsWritten = writer.WrittenCount;
        return result;
    }
}