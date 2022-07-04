using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Runtime.InteropServices;

internal static class UnixDomainSocketInterop
{
    [SupportedOSPlatform("linux")]
    internal static bool TryGetCredentials(this Socket socket, out uint processId, out uint userId, out uint groupId)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException();

        const int SOL_SOCKET = 1;
        const int SO_PEERCRED = 17;

        // ucred struct: https://github.com/torvalds/linux/blob/master/include/linux/socket.h#L173
        Span<uint> ucred = stackalloc uint[3];
        var bytesWritten = socket.GetRawSocketOption(SOL_SOCKET, SO_PEERCRED, MemoryMarshal.AsBytes(ucred));
        processId = ucred[0];
        userId = ucred[1];
        groupId = ucred[2];
        return bytesWritten == (ucred.Length * sizeof(uint));
    }
}