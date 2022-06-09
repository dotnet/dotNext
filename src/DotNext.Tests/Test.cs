using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace DotNext;

using static Buffers.BufferHelpers;

[ExcludeFromCodeCoverage]
public abstract class Test : Assert
{
    private protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private protected static byte[] RandomBytes(int size)
    {
        var result = new byte[size];
        Random.Shared.NextBytes(result);
        return result;
    }

    private static IEnumerable<ReadOnlyMemory<T>> Split<T>(ReadOnlyMemory<T> memory, int chunkSize)
    {
        var startIndex = 0;
        var length = Math.Min(chunkSize, memory.Length);

        do
        {
            yield return memory.Slice(startIndex, length);
            startIndex += chunkSize;
            length = Math.Min(memory.Length - startIndex, chunkSize);
        }
        while (startIndex < memory.Length);
    }

    private protected static ReadOnlySequence<T> ToReadOnlySequence<T>(ReadOnlyMemory<T> memory, int chunkSize)
        => Split(memory, chunkSize).ToReadOnlySequence();
}