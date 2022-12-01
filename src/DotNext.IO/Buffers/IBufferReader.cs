namespace DotNext.Buffers;

internal interface IBufferReader<out T>
{
    int RemainingBytes { get; }

    void Append(scoped ReadOnlySpan<byte> block, scoped ref int consumedBytes);

    T Complete();

    void EndOfStream() => throw new EndOfStreamException();
}