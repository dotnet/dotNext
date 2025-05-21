using Microsoft.Win32.SafeHandles;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;
using IO.Log;

partial class SimpleStateMachine
{
    private static IEnumerable<Snapshot> GetSnapshots(DirectoryInfo location)
    {
        return location.EnumerateFiles(Snapshot.SearchMask, SearchOption.TopDirectoryOnly)
            .Select(static info => new Snapshot(info));
    }
    
    private sealed class SnapshotWriter : FileWriter
    {
        private readonly string sourceFileName;
        internal readonly FileInfo Destination;

        public SnapshotWriter(long preallocationSize, DateTime creationTime, FileInfo destination)
            : base(CreateTempSnapshot(preallocationSize, creationTime, out var sourceFileName))
        {
            this.sourceFileName = sourceFileName;
            this.Destination = destination;
        }

        private static SafeFileHandle CreateTempSnapshot(long preallocationSize, DateTime creationTime, out string sourceFileName)
        {
            sourceFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var handle = File.OpenHandle(sourceFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                preallocationSize: preallocationSize);
            File.SetAttributes(handle, FileAttributes.NotContentIndexed);
            File.SetCreationTimeUtc(handle, creationTime);
            return handle;
        }

        public void Commit()
        {
            File.Move(sourceFileName, Destination.FullName, overwrite: true);
            Destination.Refresh();
        }

        public void Rollback() => File.Delete(sourceFileName);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                handle.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }

    private sealed class Snapshot : ISnapshot
    {
        internal const string SearchMask = $"*-*";
        private const char FileNameDelimiter = '-';
        internal readonly FileInfo File;

        public Snapshot(DirectoryInfo location, long index, long term)
        {
            File = CreateSnapshotFile(location, index, term);
            Index = index;
            Term = term;
        }

        public Snapshot(FileInfo file)
        {
            if (file.Name.Split(FileNameDelimiter) is not [var index, var term])
                throw new ArgumentOutOfRangeException(nameof(file));

            Index = long.Parse(index, InvariantCulture);
            Term = long.Parse(term, InvariantCulture);
            File = file;
        }

        public static FileInfo CreateSnapshotFile(DirectoryInfo location, long index, long term)
            => new(Path.Combine(location.FullName, $"{index}{FileNameDelimiter}{term}"));

        long? IDataTransferObject.Length => File.Length;

        bool IDataTransferObject.IsReusable => true;

        public DateTimeOffset Timestamp => File.CreationTimeUtc;
        
        public long Index { get; }
        
        public long Term { get; }

        public SnapshotWriter CreateWriter(long preallocationSize, DateTime creationTime)
            => new(preallocationSize, creationTime, File);

        public async ValueTask ReadFromAsync<TEntry>(TEntry entry, CancellationToken token)
            where TEntry : ILogEntry
        {
            var writer = CreateWriter(entry.Length.GetValueOrDefault(), entry.Timestamp.UtcDateTime);
            try
            {
                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
                await writer.WriteAsync(token).ConfigureAwait(false);
                writer.FlushToDisk();
            }
            finally
            {
                writer.Dispose();
                writer.Commit();
            }
        }
        
        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            var stream = File.Open(new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                BufferSize = Environment.SystemPageSize,
                Share = FileShare.Read,
            });

            try
            {
                await writer.CopyFromAsync(stream, token: token).ConfigureAwait(false);
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}