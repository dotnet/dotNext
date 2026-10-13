using System.Runtime.CompilerServices;

namespace DotNext.IO;

internal abstract class TextBufferReader : ModernTextReader
{
    private protected const int InvalidChar = -1;

    public sealed override int Read()
    {
        Unsafe.SkipInit(out char result);
        return Read(new Span<char>(ref result)) > 0 ? result : InvalidChar;
    }

    public sealed override int ReadBlock(Span<char> buffer)
    {
        int count, total = 0;
        do
        {
            count = Read(buffer.Slice(total));
            total += count;
        }
        while (count > 0);

        return total;
    }

    public sealed override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken token = default)
    {
        ValueTask<int> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<int>(token);
        }
        else
        {
            try
            {
                result = new(Read(buffer.Span));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<int>(e);
            }
        }

        return result;
    }

    public sealed override ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken token = default)
    {
        ValueTask<int> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<int>(token);
        }
        else
        {
            try
            {
                result = new(ReadBlock(buffer.Span));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<int>(e);
            }
        }

        return result;
    }

    public sealed override Task<string> ReadToEndAsync(CancellationToken token)
    {
        Task<string> result;
        try
        {
            result = Task.FromResult(ReadToEnd());
        }
        catch (Exception e)
        {
            result = Task.FromException<string>(e);
        }

        return result;
    }

    public sealed override ValueTask<string?> ReadLineAsync(CancellationToken token)
    {
        ValueTask<string?> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<string?>(token);
        }
        else
        {
            try
            {
                result = ValueTask.FromResult(ReadLine());
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<string?>(e);
            }
        }

        return result;
    }
}