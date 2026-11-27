using System.Runtime.InteropServices;

namespace DotNext.IO;

using Buffers;

partial interface IAsyncBinaryReader
{
    // the maximum size of the buffer that can be allocated before the data arrives
    internal const int MaxPreallocationSize = 64 * 1024;

    // reads the block from the source that cannot report the number of available bytes
    internal static async ValueTask<MemoryOwner<byte>> ReadAsync<TReader>(TReader reader, int length, MemoryAllocator<byte> allocator,
        CancellationToken token)
        where TReader : struct, IMemoryBlockReader
    {
        var result = allocator.AllocateExactly(int.Min(length, MaxPreallocationSize));
        try
        {
            for (int offset = 0, size = result.Length;; size = (int)long.Min(length, size * 2L))
            {
                // the buffer grows only when its previous part is filled with the received data
                result.Resize(size, allocator);
                await reader.ReadExactlyAsync(result.Memory[offset..size], token).ConfigureAwait(false);

                if ((offset = size) == length)
                    break;
            }

            result.TryResize(length);
        }
        catch
        {
            result.Dispose();
            throw;
        }

        return result;
    }

    internal interface IMemoryBlockReader
    {
        ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken token);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct BinaryBlockReader(IAsyncBinaryReader reader) : IMemoryBlockReader
    {
        ValueTask IMemoryBlockReader.ReadExactlyAsync(Memory<byte> buffer, CancellationToken token)
            => reader.ReadAsync(buffer, token);
    }
}