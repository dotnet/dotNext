using System.Buffers;
using System.Diagnostics.CodeAnalysis;

[assembly: DotNext.ReportLongRunningTests(30_000)]

namespace DotNext;

using static Buffers.Memory;

[ExcludeFromCodeCoverage]
public abstract class Test : Assert
{
    protected const string Alphabet = "abcdefghijklmnopqrstuvwxyz";
    protected const string AlphabetUpperCase = "ABCDEFGHIJKLMNOPQRSTUVWXY";
    protected const string Numbers = "0123456789";
    
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

    private protected static Action<T> Equal<T>(T expected) => actual => Equal(expected, actual);

    private protected static Action<T> Same<T>(T expected)
        where T : class
        => actual => Same(expected, actual);
}