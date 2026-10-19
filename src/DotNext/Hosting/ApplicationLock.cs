using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Hosting;

/// <summary>
/// Represents an application-level lock that can be used to allow only one running instance of the application.
/// </summary>
/// <remarks>
/// The lock is based on the exclusively opened lock file in the specified directory. Its scope is the directory:
/// processes that use the same directory compete for the same lock.
/// The lock is released automatically by the operating system when the owning process terminates, even abnormally.
/// The lock file is not deleted when the lock is released.
/// Locking is not reliable if the directory is located on the network file system (NFS or SMB).
/// </remarks>
public sealed class ApplicationLock : Disposable
{
    private readonly string lockFilePath;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private volatile SafeFileHandle? handle;

    /// <summary>
    /// Initializes a new application-level lock.
    /// </summary>
    /// <remarks>
    /// The constructor doesn't acquire the lock. Use <see cref="TryAcquire"/> to acquire it.
    /// </remarks>
    /// <param name="lockFilePath">
    /// The directory containing the lock file. If <see langword="null"/>, <see cref="AppContext.BaseDirectory"/> is used.
    /// The directory is created if it doesn't exist.
    /// </param>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have permission to create the directory.</exception>
    /// <exception cref="IOException">The directory cannot be created.</exception>
    /// <exception cref="ArgumentException"><paramref name="lockFilePath"/> doesn't contain a directory name.</exception>
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    public ApplicationLock(string? lockFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(lockFilePath))
        {
            const string lockFileExtension = ".lock";
            var fileName = Path.ChangeExtension(Assembly.GetEntryAssembly()?.GetName().Name ?? "app", lockFileExtension);
            lockFilePath = Path.Combine(AppContext.BaseDirectory, fileName);
        }

        if (Path.GetDirectoryName(lockFilePath) is not { } directory)
            throw new ArgumentException(ExceptionMessages.InvalidLockFilePath);

        Directory.CreateDirectory(directory);
        this.lockFilePath = lockFilePath;
    }

    /// <summary>
    /// Gets a value indicating that the application-level lock is supported.
    /// </summary>
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool IsSupported => !OperatingSystem.IsWasi() && !OperatingSystem.IsBrowser();

    /// <summary>
    /// Attempts to acquire the lock without blocking.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the lock is acquired successfully;
    /// <see langword="false"/> if the lock is held by another process or by another <see cref="ApplicationLock"/> instance,
    /// or if it is already held by this instance.
    /// </returns>
    /// <exception cref="ObjectDisposedException">This lock is disposed.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have permission to create or open the lock file.</exception>
    /// <exception cref="IOException">The lock file cannot be opened for reasons other than the lock conflict.</exception>
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);
        
        var handleCopy = default(SafeFileHandle);
        try
        {
            handleCopy = File.OpenHandle(lockFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            File.SetAttributes(handleCopy, FileAttributes.Hidden | FileAttributes.NotContentIndexed);
        }
        catch (IOException e) when (IsLockConflict(e))
        {
            handleCopy?.Dispose();
            return false;
        }

        handle = handleCopy;
        return true;
    }
    

    /// <summary>
    /// Releases the application lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The lock wasn't acquired by this instance.</exception>
    public void Release()
    {
        if (Interlocked.Exchange(ref handle, null) is not { } handleCopy)
            throw new SynchronizationLockException();

        handleCopy.Dispose();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Exchange(ref handle, null)?.Dispose();
        }
        
        base.Dispose(disposing);
    }

    private static bool IsLockConflict(IOException e)
    {
        const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
        const int EWOULDBLOCK_LINUX = 11;
        const int EWOULDBLOCK_BSD = 35;

        if (OperatingSystem.IsWindows())
            return e.HResult is ERROR_SHARING_VIOLATION;

        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
            return e.HResult is EWOULDBLOCK_LINUX;

        if (OperatingSystem.IsMacOS()
            || OperatingSystem.IsIOS()
            || OperatingSystem.IsTvOS()
            || OperatingSystem.IsMacCatalyst()
            || OperatingSystem.IsFreeBSD())
            return e.HResult is EWOULDBLOCK_BSD;

        throw new PlatformNotSupportedException();
    }
}