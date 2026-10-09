namespace DotNext.Text;

internal partial interface IInterpolatedStringHandler
{
    private const int MaxBufferSize = int.MaxValue / 2;
    private const long CharsPerPlaceholder = 10;
    
    void AppendLiteral(string? value);

    void AppendFormatted<T>(T value, string? format);

    void AppendFormatted(scoped ReadOnlySpan<char> value);

    void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment);

    void AppendFormatted<T>(T value, int alignment, string? format = null);
}