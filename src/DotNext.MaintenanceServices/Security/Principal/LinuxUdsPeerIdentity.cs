using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace DotNext.Security.Principal;

using Intrinsics = Runtime.Intrinsics;

/// <summary>
/// Represents identity of the peer interacting via Unix Domain Socket on Linux.
/// </summary>
/// <remarks>
/// This class is supported on Linux systems only.
/// </remarks>
[CLSCompliant(false)]
public sealed record class LinuxUdsPeerIdentity : IIdentity
{
    [SupportedOSPlatform("linux")]
    internal LinuxUdsPeerIdentity()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException();
    }

    /// <summary>
    /// Gets a value that indicates whether the user has been authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets calling process ID.
    /// </summary>
    public uint ProcessId { get; internal init; }

    /// <summary>
    /// Gets user ID of the process identified by <see cref="ProcessId"/>.
    /// </summary>
    public uint UserId { get; internal init; }

    /// <summary>
    /// Gets group ID of the process identified by <see cref="ProcessId"/>.
    /// </summary>
    public uint GroupId { get; internal init; }

    /// <inheritdoc />
    string? IIdentity.AuthenticationType => "ucred";

    /// <summary>
    /// Gets username of the process identified by <see cref="ProcessId"/>.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string? Name
    {
        get
        {
            Passwd passwd;
            string? result;
            Span<byte> buffer = stackalloc byte[Passwd.InitialBufferSize];

            if (GetPwUidR(UserId, out passwd, Intrinsics.AddressOf(MemoryMarshal.GetReference(buffer)), buffer.Length) is 0)
            {
                Debug.Assert(passwd.Name != default);
                result = Marshal.PtrToStringAnsi(passwd.Name);
            }
            else
            {
                result = null;
            }

            return result;
        }
    }

    // https://github.com/dotnet/runtime/discussions/71408
    [DllImport("libSystem.Native", EntryPoint = "SystemNative_GetPwUidR", SetLastError = false)]
    private static extern int GetPwUidR(uint uid, out Passwd pwd, nint buf, int bufLen);

    [StructLayout(LayoutKind.Sequential)]
    private struct Passwd
    {
        internal const int InitialBufferSize = 256;

        internal readonly nint Name;
        internal readonly nint Password;
        internal readonly uint UserId;
        internal readonly uint GroupId;
        internal readonly nint UserInfo;
        internal readonly nint HomeDirectory;
        internal readonly nint Shell;
    }
}