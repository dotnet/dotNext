using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
    
    protected static TResult Fork<TResult>(Func<TResult> func, [CallerMemberName] string threadName = "")
    {
        var state = new State<TResult>(func);

        var thread = new Thread(Start)
        {
            IsBackground = true,
            Name = threadName,
        };
        
        thread.UnsafeStart(state);
        True(thread.Join(DefaultTimeout));
        return state.Result;

        static void Start(object state)
        {
            var tuple = (State<TResult>)state;
            tuple.Invoke();
        }
    }
    
    private sealed class State<T>(Func<T> func)
    {
        internal T Result;

        internal void Invoke() => Result = func();
    }
}