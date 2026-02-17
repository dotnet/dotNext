using System.Text;

namespace DotNext.Text;

partial interface IInterpolatedStringHandler
{
    private const int MaxBufferSize = int.MaxValue / 2;
    private const long CharsPerPlaceholder = 10;

    private static bool IsUtf8(Encoder encoder) => ReferenceEquals(encoder.Fallback, Encoding.UTF8.EncoderFallback);
}