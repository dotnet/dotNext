namespace DotNext;

public sealed class ArgumentExceptionExtensionsTests : Test
{
    [Fact]
    public static void ThrowIfEmptyBuffer()
    {
        Throws<ArgumentException>(static () => ArgumentException.ThrowIfEmpty(Span<byte>.Empty));
        Throws<ArgumentException>(static () => ArgumentException.ThrowIfEmpty(ReadOnlySpan<byte>.Empty));
        Throws<ArgumentException>(static () => ArgumentException.ThrowIfEmpty(Memory<byte>.Empty));
        Throws<ArgumentException>(static () => ArgumentException.ThrowIfEmpty(ReadOnlyMemory<byte>.Empty));
        Throws<ArgumentException>(static () => ArgumentException.ThrowIfShorterThan(Memory<byte>.Empty, 1));
    }

    [Fact]
    public static void DoesntThrowIfNotEmptyBuffer()
    {
        Memory<byte> notEmptyArray = new byte[1];
        
        ArgumentException.ThrowIfEmpty(notEmptyArray);
        ArgumentException.ThrowIfEmpty((ReadOnlyMemory<byte>)notEmptyArray);
        
        ArgumentException.ThrowIfEmpty(notEmptyArray.Span);
        ArgumentException.ThrowIfEmpty((ReadOnlySpan<byte>)notEmptyArray.Span);
    }
}