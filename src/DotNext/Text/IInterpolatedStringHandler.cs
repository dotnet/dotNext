namespace DotNext.Text;

internal partial interface IInterpolatedStringHandler
{
    void AppendLiteral(string? value);

    void AppendFormatted<T>(T value, string? format);

    void AppendFormatted(scoped ReadOnlySpan<char> value);

    void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment);

    void AppendFormatted<T>(T value, int alignment, string? format = null);
}