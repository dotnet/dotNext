using System.Formats.Tar;

namespace DotNext.Net.Cluster.Consensus.Raft;

public partial class PersistentState
{
    private readonly TarEntryFormat backupFormat;

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
                await archive.WriteEntryAsync(file.FullName, entryName: null, token).ConfigureAwait(false);
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
                var destinationFileName = Path.Combine(destination.FullName, entry.Name);
                await entry.ExtractToFileAsync(destinationFileName, overwrite: false, token).ConfigureAwait(false);
            }
        }
        finally
        {
            await archive.DisposeAsync().ConfigureAwait(false);
        }
    }
}