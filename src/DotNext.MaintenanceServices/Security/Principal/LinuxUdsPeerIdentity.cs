using System.Runtime.Versioning;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace DotNext.Security.Principal;

/// <summary>
/// Represents identity of the peer interacting via Unix Domain Socket on Linux.
/// </summary>
/// <remarks>
/// This class is supported on Linux systems only.
/// </remarks>
[CLSCompliant(false)]
public sealed record LinuxUdsPeerIdentity : IIdentity
{
    private static readonly Getpwuid? GetpwuidFunction;

    static LinuxUdsPeerIdentity()
    {
        GetpwuidFunction = NativeLibrary.TryGetExport(NativeLibrary.GetMainProgramHandle(), "getpwuid", out var getpwuid)
            ? Marshal.GetDelegateForFunctionPointer<Getpwuid>(getpwuid)
            : null;
    }

    [SupportedOSPlatform("linux")]
    internal LinuxUdsPeerIdentity(uint pid, uint uid, uint gid)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException();

        ProcessId = pid;
        UserId = uid;
        GroupId = gid;

        if (GetpwuidFunction is not null)
        {
            ref var passwd = ref GetpwuidFunction(uid);
            if (!Unsafe.IsNullRef(ref passwd))
            {
                Name = Marshal.PtrToStringAnsi(passwd.Name);
                DisplayName = Marshal.PtrToStringAnsi(passwd.UserInfo);
            }
        }
    }

    /// <summary>
    /// Gets a value that indicates whether the user has been authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets calling process ID.
    /// </summary>
    public uint ProcessId { get; }

    /// <summary>
    /// Gets user ID of the process identified by <see cref="ProcessId"/>.
    /// </summary>
    public uint UserId { get; }

    /// <summary>
    /// Gets group ID of the process identified by <see cref="ProcessId"/>.
    /// </summary>
    public uint GroupId { get; }

    /// <inheritdoc />
    string? IIdentity.AuthenticationType => "ucred";

    /// <inheritdoc />
    public string? Name { get; }

    /// <summary>
    /// Gets user information, if available.
    /// </summary>
    public string? DisplayName { get; }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Passwd
    {
        internal readonly nint Name;
        internal readonly nint Password;
        internal readonly uint UserId;
        internal readonly uint GroupId;
        internal readonly nint UserInfo;
        internal readonly nint HomeDirectory;
        internal readonly nint Shell;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ref Passwd Getpwuid(uint userId);
}