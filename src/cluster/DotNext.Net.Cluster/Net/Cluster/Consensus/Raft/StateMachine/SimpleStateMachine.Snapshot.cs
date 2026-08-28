using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;
using IO.Log;

partial class SimpleStateMachine
{
    private IEnumerable<Snapshot> GetSnapshots()
        => location.EnumerateFiles(Snapshot.SearchMask, SearchOption.TopDirectoryOnly)
            .Select(CreateSnapshot);

    private Snapshot CreateSnapshot(FileInfo snapshotFile) => new(snapshotFile, writerFactory);

    private static unsafe Func<long, FileInfo, SnapshotWriter> CreateSnapshotWriterFactory()
    {
        if (OperatingSystem.IsLinux()
            && NativeLibrary.TryGetExport(NativeLibrary.GetMainProgramHandle(), "open", out var funcPtr))
        {
            return LinuxSnapshotWriter.CreateFactory((delegate*unmanaged<byte*, int, int, int>)funcPtr);
        }

        return SnapshotWriter.CreateDefault;
    }

    private class SnapshotWriter : FileWriter
    {
        private readonly string sourceFileName;
        internal readonly FileInfo Destination;

        protected SnapshotWriter(long preallocationSize, FileInfo destination)
            : base(CreateTempSnapshot(preallocationSize, destination.DirectoryName, out var sourceFileName))
        {
            this.sourceFileName = sourceFileName;
            Destination = destination;
        }

        public static SnapshotWriter CreateDefault(long preallocationSize, FileInfo destination)
            => new(preallocationSize, destination);

        private static SafeFileHandle CreateTempSnapshot(long preallocationSize, ReadOnlySpan<char> directory, out string sourceFileName)
        {
            // Source file must be on the same file system for atomicity
            sourceFileName = Path.Combine(directory.ToString(), string.Concat(Path.GetRandomFileName(), ".tmp"));
            var handle = File.OpenHandle(sourceFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                preallocationSize: preallocationSize);
            File.SetAttributes(handle, FileAttributes.NotContentIndexed);
            return handle;
        }

        protected virtual void Commit(string sourceFileName, string destinationFileName)
            => File.Move(sourceFileName, destinationFileName, overwrite: true);

        public void Commit()
        {
            Commit(sourceFileName, Destination.FullName);
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
        private readonly Func<long, FileInfo, SnapshotWriter> writerFactory;

        public Snapshot(DirectoryInfo location, long index, long term, Func<long, FileInfo, SnapshotWriter> writerFactory)
        {
            File = CreateSnapshotFile(location, index, term);
            Index = index;
            Term = term;
            this.writerFactory = writerFactory;
        }

        public Snapshot(FileInfo file, Func<long, FileInfo, SnapshotWriter> writerFactory)
        {
            if (file.Name.Split(FileNameDelimiter) is not [var index, var term])
                throw new ArgumentOutOfRangeException(nameof(file));

            Index = long.Parse(index, InvariantCulture);
            Term = long.Parse(term, InvariantCulture);
            File = file;
            this.writerFactory = writerFactory;
        }

        public static FileInfo CreateSnapshotFile(DirectoryInfo location, long index, long term)
            => new(Path.Combine(location.FullName, $"{index}{FileNameDelimiter}{term}"));

        long? IDataTransferObject.Length => File.Length;

        bool IDataTransferObject.IsReusable => true;
        
        public long Index { get; }
        
        public long Term { get; }

        private SnapshotWriter CreateWriter(long preallocationSize)
            => writerFactory(preallocationSize, File);

        public async ValueTask ReadFromAsync<TEntry>(TEntry entry, CancellationToken token)
            where TEntry : ILogEntry
        {
            var writer = CreateWriter(entry.Length.GetValueOrDefault());
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
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
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