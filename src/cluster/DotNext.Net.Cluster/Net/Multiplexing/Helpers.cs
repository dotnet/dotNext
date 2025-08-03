using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

internal static class Helpers
{
    public static async ValueTask DisconnectAsync(this Socket socket, TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);
        try
        {
            await socket.DisconnectAsync(reuseSocket: false, cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // suppress the timeout
        }
        finally
        {
            cts.Dispose();
            socket.Dispose();
        }
    }

    public static bool IsNotCompleted(this Task task) => task.IsCompleted is false;
}