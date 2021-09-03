using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO
{
    internal abstract class TextBufferReader : TextReader
    {
        private protected const int InvalidChar = -1;

        public sealed override int Read()
        {
            var result = default(char);
            return Read(MemoryMarshal.CreateSpan(ref result, 1)) > 0 ? result : InvalidChar;
        }

        public sealed override int Read(char[] buffer, int index, int count)
            => Read(buffer.AsSpan(index, count));

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

        public sealed override int ReadBlock(char[] buffer, int index, int count)
            => ReadBlock(buffer.AsSpan(index, count));

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

        public sealed override Task<int> ReadAsync(char[] buffer, int index, int count)
            => ReadAsync(buffer.AsMemory(index, count)).AsTask();

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

        public sealed override Task<int> ReadBlockAsync(char[] buffer, int index, int count)
            => ReadBlockAsync(buffer.AsMemory(index, count)).AsTask();

        public sealed override Task<string> ReadToEndAsync()
        {
            Task<string> result;
            try
            {
                result = Task.FromResult<string>(ReadToEnd());
            }
            catch (Exception e)
            {
                result = Task.FromException<string>(e);
            }

            return result;
        }

        public sealed override Task<string?> ReadLineAsync()
        {
            Task<string?> result;
            try
            {
                result = Task.FromResult<string?>(ReadLine());
            }
            catch (Exception e)
            {
                result = Task.FromException<string?>(e);
            }

            return result;
        }
    }
}