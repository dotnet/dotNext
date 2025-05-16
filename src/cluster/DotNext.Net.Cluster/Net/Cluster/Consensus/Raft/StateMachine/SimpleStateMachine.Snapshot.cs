using System.Runtime.InteropServices;
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
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct SnapshotWriter : IDisposable
    {
        private readonly SafeFileHandle handle;
        private readonly string sourceFileName;
        private readonly FileInfo destination;
        internal readonly FileWriter Output;

        internal SnapshotWriter(long preallocationSize, DateTime creationTime, FileInfo destination)
        {
            sourceFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            handle = File.OpenHandle(sourceFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, preallocationSize: preallocationSize);
            File.SetAttributes(handle, FileAttributes.NotContentIndexed);
            File.SetCreationTimeUtc(handle, creationTime);
            Output = new(handle) { MaxBufferSize = Environment.SystemPageSize };
            this.destination = destination;
        }

        public void Dispose()
        {
            Output.Dispose();
            handle.Dispose();
            File.Move(sourceFileName, destination.FullName, overwrite: true);
            destination.Refresh();
        }
    }

    private sealed class Snapshot : ISnapshot
    {
        internal const string SearchMask = $"*-*";
        private const char FileNameDelimiter = '-';
        internal readonly FileInfo File;

        public Snapshot(DirectoryInfo location, long index, long term)
        {
            File = new(Path.Combine(location.FullName, $"{index}{FileNameDelimiter}{term}"));
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
                await entry.WriteToAsync(writer.Output, token).ConfigureAwait(false);
                await writer.Output.WriteAsync(token).ConfigureAwait(false);
                writer.Output.FlushToDisk();
            }
            finally
            {
                writer.Dispose();
                File.Refresh();
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