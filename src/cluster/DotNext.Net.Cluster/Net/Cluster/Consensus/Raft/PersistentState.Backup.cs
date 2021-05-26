using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    public partial class PersistentState
    {
        private readonly CompressionLevel backupCompression;

        /// <summary>
        /// Creates backup of this audit trail.
        /// </summary>
        /// <param name="output">The stream used to store backup.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>A task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async Task CreateBackupAsync(Stream output, CancellationToken token = default)
        {
            ZipArchive? archive = null;
            await syncRoot.AcquireAsync(LockType.ReadLock, token).ConfigureAwait(false);
            try
            {
                archive = new(output, ZipArchiveMode.Create, true);
                foreach (var file in location.EnumerateFiles())
                {
                    await using var source = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, true);
                    var entry = archive.CreateEntry(file.Name, backupCompression);
                    entry.LastWriteTime = file.LastWriteTime;
                    await using var destination = entry.Open();
                    await source.CopyToAsync(destination, token).ConfigureAwait(false);
                }

                await output.FlushAsync(token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.Release(LockType.ReadLock);
                archive?.Dispose();
            }
        }

        /// <summary>
        /// Restores persistent state from backup.
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
            using var archive = new ZipArchive(backup, ZipArchiveMode.Read, true);
            foreach (var entry in archive.Entries)
            {
                await using var fs = new FileStream(Path.Combine(destination.FullName, entry.Name), FileMode.Create, FileAccess.Write, FileShare.None, 1024, true);
                await using var entryStream = entry.Open();
                await entryStream.CopyToAsync(fs, token).ConfigureAwait(false);
            }
        }
    }
}