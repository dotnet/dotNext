﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;

    public partial class PersistentState
    {
        internal readonly struct LogEntryMetadata
        {
            internal readonly long Term, Timestamp, Length, Offset;

            private LogEntryMetadata(DateTimeOffset timeStamp, long term, long offset, long length)
            {
                Term = term;
                Timestamp = timeStamp.UtcTicks;
                Length = length;
                Offset = offset;
            }

            internal static LogEntryMetadata Create<TLogEntry>(TLogEntry entry, long offset, long length)
                where TLogEntry : IRaftLogEntry
                => new LogEntryMetadata(entry.Timestamp, entry.Term, offset, length);

            internal static int Size => Unsafe.SizeOf<LogEntryMetadata>();
        }

        internal readonly struct SnapshotMetadata
        {
            internal readonly long Index;
            internal readonly LogEntryMetadata RecordMetadata;

            private SnapshotMetadata(LogEntryMetadata metadata, long index)
            {
                Index = index;
                RecordMetadata = metadata;
            }

            internal static SnapshotMetadata Create<TLogEntry>(TLogEntry snapshot, long index, long length)
                where TLogEntry : IRaftLogEntry
                => new SnapshotMetadata(LogEntryMetadata.Create(snapshot, Size, length), index);

            internal static int Size => Unsafe.SizeOf<SnapshotMetadata>();
        }

        private abstract class ConcurrentStorageAccess : Stream, IFlushable
        {
            // do not derive from FileStream because some virtual methods
            // assumes that they are overridden and do async calls inefficiently
            private readonly FileStream fs;
            private readonly StreamSegment[] readers;   // a pool of read-only streams that can be shared between multiple readers in parallel

            private protected ConcurrentStorageAccess(string fileName, int bufferSize, int readersCount, FileOptions options)
            {
                fs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, bufferSize, options);
                readers = new StreamSegment[readersCount];
                if (readersCount == 1)
                {
                    readers[0] = new StreamSegment(fs, true);
                }
                else
                {
                    foreach (ref var reader in readers.AsSpan())
                        reader = new StreamSegment(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous | FileOptions.RandomAccess), false);
                }
            }

            public sealed override bool CanRead => fs.CanRead;

            public sealed override bool CanWrite => fs.CanWrite;

            public sealed override bool CanSeek => fs.CanSeek;

            public sealed override bool CanTimeout => fs.CanTimeout;

            public sealed override long Length => fs.Length;

            public sealed override long Position
            {
                get => fs.Position;
                set => fs.Position = value;
            }

            public sealed override int ReadTimeout
            {
                get => fs.ReadTimeout;
                set => fs.ReadTimeout = value;
            }

            public sealed override int WriteTimeout
            {
                get => fs.WriteTimeout;
                set => fs.WriteTimeout = value;
            }

            public sealed override void SetLength(long length) => fs.SetLength(length);

            public sealed override long Seek(long offset, SeekOrigin origin)
                => fs.Seek(offset, origin);

            public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
                => fs.BeginRead(buffer, offset, count, callback, state);

            public sealed override int EndRead(IAsyncResult asyncResult)
                => fs.EndRead(asyncResult);

            public sealed override int Read(Span<byte> buffer)
                => fs.Read(buffer);

            public sealed override int Read(byte[] buffer, int offset, int count)
                => fs.Read(buffer, offset, count);

            public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
                => fs.ReadAsync(buffer, offset, count, token);

            public sealed override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
                => fs.ReadAsync(buffer, token);

            public sealed override int ReadByte() => fs.ReadByte();

            public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
                => fs.BeginWrite(buffer, offset, count, callback, state);

            public sealed override void EndWrite(IAsyncResult asyncResult)
                => fs.EndWrite(asyncResult);

            public sealed override void Write(ReadOnlySpan<byte> buffer)
                => fs.Write(buffer);

            public sealed override void Write(byte[] buffer, int offset, int count)
                => fs.Write(buffer, offset, count);

            public sealed override void WriteByte(byte value)
                => fs.WriteByte(value);

            public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => fs.WriteAsync(buffer, offset, count, cancellationToken);

            public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
                => fs.WriteAsync(buffer, cancellationToken);

            public sealed override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
                => fs.CopyToAsync(destination, bufferSize, cancellationToken);

            public sealed override void CopyTo(Stream destination, int bufferSize)
                => fs.CopyTo(destination, bufferSize);

            public sealed override Task FlushAsync(CancellationToken token = default) => fs.FlushAsync(token);

            public sealed override void Flush() => fs.Flush(true);

            internal string FileName => fs.Name;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private protected StreamSegment GetReadSessionStream(in DataAccessSession session) => readers[session.SessionId];

            internal Task FlushAsync(in DataAccessSession session, CancellationToken token)
                => GetReadSessionStream(session).FlushAsync(token);

            internal abstract void PopulateCache(in DataAccessSession session);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    foreach (ref var reader in readers.AsSpan())
                    {
                        reader?.Dispose();
                        reader = null;
                    }

                    fs.Dispose();
                }

                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                foreach (Stream? reader in readers)
                {
                    if (reader is not null)
                        await reader.DisposeAsync().ConfigureAwait(false);
                }

                Array.Clear(readers, 0, readers.Length);
                await fs.DisposeAsync().ConfigureAwait(false);
                base.Dispose(true);
            }
        }
    }
}
