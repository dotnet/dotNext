using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Buffers.Binary;

partial class WriteAheadLog
{
    [StructLayout(LayoutKind.Auto)]
    private struct Checkpoint : IDisposable
    {
        private const uint CurrentVersion = CheckpointVersion0.Version;
        
        private const int VersionLength = sizeof(uint);
        private const int MaxSize = CheckpointVersion0.Size + VersionLength;
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
                        Debug.Assert(CurrentVersion == CheckpointVersion0.Version);
                        
                        Version = CurrentVersion;
                        checkpoint = new CheckpointVersion0(checkpoint: 0L);
                        break;
                    case sizeof(long):
                        Version = CheckpointVersion0.Version;
                        checkpoint = new CheckpointVersion0(BinaryPrimitives.ReadInt64LittleEndian(readBuf));
                        break;
                    default:
                        checkpoint = (Version = BinaryPrimitives.ReadUInt32LittleEndian(readBuf)) switch
                        {
                            CheckpointVersion0.Version => CheckpointVersion0.Parse(readBuf.Slice(VersionLength)),
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
        long Checkpoint { get; }
        
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
    
    /// <summary>
    /// Represents catastrophic WAL failure.
    /// </summary>
    public abstract class IntegrityException : Exception
    {
        private protected IntegrityException()
        {
        }
    }
    
    public sealed class UnsupportedVersionException : IntegrityException
    {
        internal UnsupportedVersionException(uint actualVersion)
        {
            
        }
    }
}