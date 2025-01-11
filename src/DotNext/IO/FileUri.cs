using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    private const string UncPrefix = @"\\";
    private const char DriveSeparatorChar = ':';

    private static readonly SearchValues<char> Delimiters =
        SearchValues.Create(OperatingSystem.IsWindows() ? [Path.DirectorySeparatorChar, DriveSeparatorChar] : [Path.DirectorySeparatorChar]);

    /// <summary>
    /// Encodes file name as URI.
    /// </summary>
    /// <param name="fileName">The fully-qualified file name.</param>
    /// <param name="settings">The encoding settings.</param>
    /// <returns><paramref name="fileName"/> as URI. The return value can be passed to <see cref="Uri(string)"/> constructor.</returns>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is not fully-qualified.</exception>
    public static string Encode(ReadOnlySpan<char> fileName, TextEncoderSettings? settings = null)
    {
        ThrowIfPartiallyQualified(fileName);
        var encoder = settings is null ? UrlEncoder.Default : UrlEncoder.Create(settings);
        var maxLength = GetMaxEncodedLengthCore(fileName, encoder);
        using var buffer = (uint)maxLength <= (uint)SpanOwner<char>.StackallocThreshold
            ? stackalloc char[maxLength]
            : new SpanOwner<char>(maxLength);

        return TryEncodeCore(fileName, encoder, buffer.Span, out var writtenCount)
            ? new(buffer.Span.Slice(0, writtenCount))
            : string.Empty;
    }

    /// <summary>
    /// Gets the maximum number of characters that can be produced by <see cref="TryEncode"/> method.
    /// </summary>
    /// <param name="fileName">The file name to be encoded.</param>
    /// <param name="encoder">The encoder.</param>
    /// <returns>The maximum number of characters that can be produced by the encoder.</returns>
    public static int GetMaxEncodedLength(ReadOnlySpan<char> fileName, UrlEncoder? encoder = null)
        => GetMaxEncodedLengthCore(fileName, encoder ?? UrlEncoder.Default);

    private static int GetMaxEncodedLengthCore(ReadOnlySpan<char> fileName, UrlEncoder encoder)
        => FileScheme.Length
           + Unsafe.BitCast<bool, byte>(OperatingSystem.IsWindows())
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
    public static bool TryEncode(ReadOnlySpan<char> fileName, UrlEncoder? encoder, Span<char> output, out int charsWritten)
    {
        ThrowIfPartiallyQualified(fileName);

        return TryEncodeCore(fileName, encoder ?? UrlEncoder.Default, output, out charsWritten);
    }

    [StackTraceHidden]
    private static void ThrowIfPartiallyQualified(ReadOnlySpan<char> fileName)
    {
        if (!Path.IsPathFullyQualified(fileName))
            throw new ArgumentException(ExceptionMessages.FullyQualifiedPathExpected, nameof(fileName));
    }

    private static bool TryEncodeCore(ReadOnlySpan<char> fileName, UrlEncoder encoder, Span<char> output, out int charsWritten)
    {
        const char slash = '/';
        const char escapedDriveSeparatorChar = '|';
        var result = false;
        var writer = new SpanWriter<char>(output);
        writer.Write(FileScheme);
        if (!OperatingSystem.IsWindows())
        {
            // nothing to do
        }
        else if (fileName.StartsWith(UncPrefix))
        {
            fileName = fileName.Slice(UncPrefix.Length);
        }
        else
        {
            writer.Add(slash);
        }

        while (!fileName.IsEmpty)
        {
            var index = fileName.IndexOfAny(Delimiters);
            char replacement;
            ReadOnlySpan<char> component;
            if (index >= 0)
            {
                component = fileName.Slice(0, index);
                
                // skip bounds check
                replacement = OperatingSystem.IsWindows() && Unsafe.Add(ref MemoryMarshal.GetReference(fileName), index) is DriveSeparatorChar
                    ? escapedDriveSeparatorChar
                    : slash;
                fileName = fileName.Slice(index + 1);
            }
            else
            {
                component = fileName;
                fileName = default;
                replacement = '\0';
            }

            result = encoder.Encode(component, writer.RemainingSpan, out _, out charsWritten) is OperationStatus.Done;
            if (!result)
                break;

            writer.Advance(charsWritten);
            if (replacement is not '\0')
                writer.Add(replacement);
        }

        charsWritten = writer.WrittenCount;
        return result;
    }
}