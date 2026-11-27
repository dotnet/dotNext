using System.IO.Pipelines;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;
using Pipelines;

partial class AsyncBinaryReaderWriterTests
{
    public interface IAsyncBinaryReaderWriterSource : IAsyncDisposable
    {
        IAsyncBinaryWriter CreateWriter();

        IAsyncBinaryReader CreateReader();
    }

    private sealed class DefaultSource : IAsyncBinaryReaderWriterSource
    {
        private sealed class DefaultAsyncBinaryReader(Stream stream, Memory<byte> buffer) : IAsyncBinaryReader
        {
            private readonly IAsyncBinaryReader reader = IAsyncBinaryReader.Create(stream, buffer);

            ValueTask<TReader> IAsyncBinaryReader.ReadAsync<TReader>(TReader reader, CancellationToken token)
                => this.reader.ReadAsync(reader, token);

            ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, long? count, CancellationToken token)
                => reader.CopyToAsync(consumer, count, token);
        }

        private sealed class DefaultAsyncBinaryWriter(Stream stream, Memory<byte> buffer) : IAsyncBinaryWriter
        {
            private readonly IAsyncBinaryWriter writer = IAsyncBinaryWriter.Create(stream, buffer);

            Memory<byte> IAsyncBinaryWriter.Buffer => writer.Buffer;

            ValueTask IAsyncBinaryWriter.AdvanceAsync(int bytesWritten, CancellationToken token)
                => writer.AdvanceAsync(bytesWritten, token);
        }

        private readonly MemoryStream stream = new();
        private readonly byte[] buffer = new byte[512];

        public IAsyncBinaryReader CreateReader()
        {
            stream.Position = 0;
            return new DefaultAsyncBinaryReader(stream, buffer);
        }

        public IAsyncBinaryWriter CreateWriter() => new DefaultAsyncBinaryWriter(stream, buffer);

        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }

    private sealed class BufferSource : IAsyncBinaryReaderWriterSource
    {
        private readonly PoolingBufferWriter<byte> buffer = new();

        public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(buffer);

        public IAsyncBinaryReader CreateReader() => new SequenceReader(buffer.WrittenMemory);

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            buffer.Dispose();
            return new ValueTask();
        }
    }

    private sealed class StreamSource : IAsyncBinaryReaderWriterSource
    {
        private readonly MemoryStream stream = new(1024);
        private readonly byte[] buffer = new byte[512];

        public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream, buffer);

        public IAsyncBinaryReader CreateReader()
        {
            stream.Position = 0L;
            return IAsyncBinaryReader.Create(stream, buffer);
        }

        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }

    private sealed class PipeSource : IAsyncBinaryReaderWriterSource, IFlushable
    {
        private readonly Pipe pipe = new();

        public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(pipe.Writer, bufferSize: 16L);

        public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(pipe.Reader);

        internal ValueTask CompleteWriterAsync() => pipe.Writer.CompleteAsync();

        public Task FlushAsync(CancellationToken token)
            => pipe.Writer.FlushAsync(token).AsTask();

        void IFlushable.Flush() => throw new NotSupportedException();

        public async ValueTask DisposeAsync()
        {
            await pipe.Writer.CompleteAsync();
            await pipe.Reader.CompleteAsync();
            pipe.Reset();
        }
    }

    private sealed class ReadOnlySequenceSource : IAsyncBinaryReaderWriterSource
    {
        private readonly MemoryStream stream = new(1024);
        private readonly byte[] buffer = new byte[512];

        public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream, buffer);

        public IAsyncBinaryReader CreateReader()
        {
            stream.Position = 0L;
            var sequence = ToReadOnlySequence<byte>(stream.ToArray(), 3);
            return new SequenceReader(sequence);
        }

        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }

    private sealed class FileSource : Disposable, IAsyncBinaryReaderWriterSource, IFlushable
    {
        private readonly SafeFileHandle handle;
        private readonly FileWriter writer;
        private readonly FileReader reader;

        public FileSource(int bufferSize)
        {
            var path = GetTempPath();
            handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            writer = new(handle) { MaxBufferSize = bufferSize };
            reader = new(handle) { MaxBufferSize = bufferSize };
        }

        public IAsyncBinaryWriter CreateWriter() => writer;

        public IAsyncBinaryReader CreateReader() => reader;

        public Task FlushAsync(CancellationToken token) => writer.WriteAsync(token).AsTask();

        void IFlushable.Flush() => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writer.Dispose();
                reader.Dispose();
                handle.Dispose();
            }

            base.Dispose(disposing);
        }

        public new ValueTask DisposeAsync() => base.DisposeAsync();
    }
    
    private sealed class BufferedFileSource : Disposable, IAsyncBinaryReaderWriterSource, IFlushable
    {
        private readonly SafeFileHandle handle;
        private readonly PoolingBufferedStream writer;
        private readonly PoolingBufferedStream reader;

        public BufferedFileSource(int bufferSize)
        {
            var path = GetTempPath();
            handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            writer = new(handle.AsUnbufferedStream(FileAccess.Write)) { MaxBufferSize = bufferSize };
            reader = new(handle.AsUnbufferedStream(FileAccess.Read)) { MaxBufferSize = bufferSize };
        }

        public IAsyncBinaryWriter CreateWriter() => writer;

        public IAsyncBinaryReader CreateReader() => reader;

        public Task FlushAsync(CancellationToken token) => writer.FlushAsync(token);

        void IFlushable.Flush() => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writer.Dispose();
                reader.Dispose();
                handle.Dispose();
            }

            base.Dispose(disposing);
        }

        public new ValueTask DisposeAsync() => base.DisposeAsync();
    } 
}