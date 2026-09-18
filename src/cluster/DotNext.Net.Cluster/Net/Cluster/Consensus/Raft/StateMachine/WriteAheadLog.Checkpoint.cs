using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Buffers.Binary;
using IO.Log;

partial class WriteAheadLog
{
    [StructLayout(LayoutKind.Auto)]
    private struct Checkpoint : IDisposable
    {
        private const uint CurrentVersion = CheckpointVersion1.Version;
        
        private const int VersionLength = sizeof(uint);
        private const int MaxSize = CheckpointVersion1.Size + VersionLength;
        private const string FileName = "checkpoint";

        private readonly SafeFileHandle handle;
        private readonly byte[] buffer;
        internal readonly uint Version;

        internal Checkpoint(DirectoryInfo location, out IVersionedCheckpoint? checkpoint)
        {
            var path = Path.Combine(location.FullName, FileName);

            // read the checkpoint
            using (var readHandle = File.OpenHandle(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                Span<byte> readBuf = stackalloc byte[MaxSize];
                switch (RandomAccess.Read(readHandle, readBuf, 0L))
                {
                    case 0:
                        Debug.Assert(CurrentVersion == CheckpointVersion1.Version);
                        
                        Version = CurrentVersion;
                        checkpoint = new CheckpointVersion1(commitIndex: 0L, lastIndex: 0L);
                        break;
                    case CheckpointVersion0.Size:
                        Version = CheckpointVersion0.Version;
                        checkpoint = new CheckpointVersion0(BinaryPrimitives.ReadInt64LittleEndian(readBuf));
                        break;
                    default:
                        Version = BinaryPrimitives.ReadUInt32LittleEndian(readBuf);
                        readBuf = readBuf.Slice(VersionLength);
                        checkpoint = Version switch
                        {
                            CheckpointVersion0.Version => CheckpointVersion0.Parse(readBuf),
                            CheckpointVersion1.Version => CheckpointVersion1.Parse(readBuf),
                            _ => null
                        };

                        break;
                }
            }

            handle = File.OpenHandle(path, FileMode.Open, FileAccess.Write, options: FileOptions.Asynchronous | FileOptions.WriteThrough);
            buffer = GC.AllocateArray<byte>(MaxSize, pinned: true);
        }

        public ValueTask UpdateAsync<TCheckpoint>(TCheckpoint checkpoint, CancellationToken token)
            where TCheckpoint : struct, IBinaryFormattable<TCheckpoint>, IVersionedCheckpoint
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, VersionLength), TCheckpoint.Version);
            checkpoint.Format(buffer.AsSpan(VersionLength));
            return RandomAccess.WriteAsync(handle, buffer.AsMemory(0, VersionLength + TCheckpoint.Size), fileOffset: 0L, token);
        }

        public void Dispose()
        {
            handle?.Dispose();
            this = default;
        }
    }
    
    private interface IVersionedCheckpoint
    {
        static abstract uint Version { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct CheckpointVersion0(long checkpoint) : IBinaryFormattable<CheckpointVersion0>, IVersionedCheckpoint
    {
        public const uint Version = 0;
        public const int Size = sizeof(long);

        static int IBinaryFormattable<CheckpointVersion0>.Size => Size;
        
        public void Format(scoped Span<byte> destination)
        {
            var writer = new SpanWriter<byte>(destination);
            writer.WriteLittleEndian(checkpoint);
        }

        public static CheckpointVersion0 Parse(scoped ReadOnlySpan<byte> source)
        {
            var reader = new SpanReader<byte>(source);
            return new(reader.ReadLittleEndian<long>());
        }

        public long Checkpoint => checkpoint;

        static uint IVersionedCheckpoint.Version => Version;
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct CheckpointVersion1(long commitIndex, long lastIndex) : IBinaryFormattable<CheckpointVersion1>, IVersionedCheckpoint
    {
        public const uint Version = 1;
        public const int Size = sizeof(long) + sizeof(long);

        static int IBinaryFormattable<CheckpointVersion1>.Size => Size;
        
        public void Format(scoped Span<byte> destination)
        {
            var writer = new SpanWriter<byte>(destination);
            writer.WriteLittleEndian(commitIndex);
            writer.WriteLittleEndian(lastIndex);
        }

        public static CheckpointVersion1 Parse(scoped ReadOnlySpan<byte> source)
        {
            var reader = new SpanReader<byte>(source);
            return new(reader.ReadLittleEndian<long>(),
                reader.ReadLittleEndian<long>());
        }

        public long CommitIndex => commitIndex;

        public long LastIndex => lastIndex;

        static uint IVersionedCheckpoint.Version => Version;
    }
    
    /// <summary>
    /// Indicates that the checkpoint file has unsupported version.
    /// </summary>
    public sealed class UnsupportedCheckpointVersionException : IntegrityException
    {
        internal UnsupportedCheckpointVersionException(uint actualVersion)
            : base(ExceptionMessages.BadCheckpointVersion(actualVersion))
            => Version = actualVersion;
        
        /// <summary>
        /// Gets the actual version that is not supported.
        /// </summary>
        [CLSCompliant(false)]
        public uint Version { get; }
    }
}