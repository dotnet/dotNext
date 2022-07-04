using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace DotNext.Maintenance;

using Buffers;

internal static class SocketWriter
{
    internal static void Flush(this Socket socket, BufferWriter<byte> buffer)
    {
        try
        {
            Write(socket, buffer.WrittenMemory.Span);
        }
        finally
        {
            buffer.Clear(reuseBuffer: true);
        }

        static void Write(Socket socket, ReadOnlySpan<byte> buffer)
        {
            for (int count; !buffer.IsEmpty; buffer = buffer.Slice(count))
            {
                count = socket.Send(buffer);
            }
        }
    }

    internal static Task FlushAsync(this Socket socket, BufferWriter<byte> buffer, CancellationToken token)
    {
        return buffer.WrittenCount > 0 ? FlushCoreAsync() : Task.CompletedTask;

        async Task FlushCoreAsync()
        {
            try
            {
                await WriteAsync(socket, buffer.WrittenMemory, token).ConfigureAwait(false);
            }
            finally
            {
                buffer.Clear(reuseBuffer: true);
            }
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask WriteAsync(Socket socket, ReadOnlyMemory<byte> buffer, CancellationToken token)
        {
            for (int count; !buffer.IsEmpty; buffer = buffer.Slice(count))
            {
                count = await socket.SendAsync(buffer, SocketFlags.None, token).ConfigureAwait(false);
            }
        }
    }
}