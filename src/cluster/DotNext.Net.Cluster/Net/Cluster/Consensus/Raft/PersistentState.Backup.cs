using System.Diagnostics;
using System.Formats.Tar;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Net.Cluster.Consensus.Raft;

public partial class PersistentState
{
    private readonly TarEntryFormat backupFormat;

    private TarEntry CreateTarEntry(FileInfo source)
    {
        TarEntry destination = backupFormat switch
        {
            TarEntryFormat.Gnu => new GnuTarEntry(TarEntryType.RegularFile, source.Name) { AccessTime = source.LastAccessTime, ChangeTime = source.LastWriteTime, UserName = Environment.UserName },
            TarEntryFormat.Ustar => new UstarTarEntry(TarEntryType.RegularFile, source.Name) { UserName = Environment.UserName },
            TarEntryFormat.V7 => new V7TarEntry(TarEntryType.RegularFile, source.Name),
            _ => new PaxTarEntry(TarEntryType.RegularFile, source.Name) { UserName = Environment.UserName },
        };

        destination.ModificationTime = source.LastWriteTime;

        if (Environment.OSVersion is { Platform: PlatformID.Unix })
        {
            destination.Mode = source.UnixFileMode;
        }

        return destination;
    }

    private static void ImportAttributes(SafeFileHandle handle, TarEntry entry)
    {
        switch (entry)
        {
            case GnuTarEntry gnu:
                File.SetLastAccessTimeUtc(handle, gnu.AccessTime.UtcDateTime);
                goto default;
            default:
                File.SetLastWriteTimeUtc(handle, entry.ModificationTime.UtcDateTime);
                break;
        }

        if (Environment.OSVersion is { Platform: PlatformID.Unix })
        {
            Debug.Assert(!OperatingSystem.IsWindows());

            File.SetUnixFileMode(handle, entry.Mode);
        }
    }

    /// <summary>
    /// Creates backup of this audit trail in TAR format.
    /// </summary>
    /// <param name="output">The stream used to store backup.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async Task CreateBackupAsync(Stream output, CancellationToken token = default)
    {
        TarWriter? archive = null;
        await syncRoot.AcquireAsync(LockType.StrongReadLock, token).ConfigureAwait(false);
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            };

            archive = new(output, backupFormat, leaveOpen: true);
            foreach (var file in Location.EnumerateFiles())
            {
                var destination = CreateTarEntry(file);
                var source = file.Open(options);
                try
                {
                    destination.DataStream = source;
                    await archive.WriteEntryAsync(destination, token).ConfigureAwait(false);
                }
                finally
                {
                    await source.DisposeAsync().ConfigureAwait(false);
                }
            }

            await output.FlushAsync(token).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.StrongReadLock);
            if (archive is not null)
                await archive.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Restores persistent state from backup represented in TAR format.
    /// </summary>
    /// <remarks>
    /// All files within destination directory will be deleted
    /// permanently.
    /// </remarks>
    /// <param name="backup">The stream containing backup.</param>
    /// <param name="destination">The destination directory.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task RestoreFromBackupAsync(Stream backup, DirectoryInfo destination, CancellationToken token = default)
    {
        // cleanup directory
        foreach (var file in destination.EnumerateFiles())
            file.Delete();

        // extract files from archive
        var archive = new TarReader(backup, leaveOpen: true);
        try
        {
            while (await archive.GetNextEntryAsync(copyData: false, token).ConfigureAwait(false) is { } entry)
            {
                var sourceStream = entry.DataStream;
                var destinationStream = new FileStream(Path.Combine(destination.FullName, entry.Name), FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                try
                {
                    ImportAttributes(destinationStream.SafeFileHandle, entry);

                    if (sourceStream is not null)
                    {
                        await sourceStream.CopyToAsync(destinationStream, token).ConfigureAwait(false);
                        await destinationStream.FlushAsync(token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (sourceStream is not null)
                        await sourceStream.DisposeAsync().ConfigureAwait(false);

                    await destinationStream.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await archive.DisposeAsync().ConfigureAwait(false);
        }
    }
}