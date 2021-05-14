using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.IO
{
    using Buffers;
    using static Runtime.Intrinsics;

    public partial class FileBufferingWriter
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct BackingFileProvider : ISupplier<FileStream>
        {
            // if temporary == true then contains path to the directory;
            // otherwise, contains full path to the file
            private readonly string path;
            private readonly bool temporary;
            private readonly FileOptions options;
            internal readonly int BufferSize;

            private BackingFileProvider(string path, bool temp, int bufferSize, bool asyncIO, bool writeThrough)
            {
                var options = FileOptions.SequentialScan;

                if (temp)
                    options |= FileOptions.DeleteOnClose;
                if (asyncIO)
                    options |= FileOptions.Asynchronous;
                if (writeThrough)
                    options |= FileOptions.WriteThrough;

                this.options = options;
                this.path = path;
                BufferSize = bufferSize;
                temporary = temp;
            }

            internal BackingFileProvider(in Options options)
                : this(options.Path, options.UseTemporaryFile, options.FileBufferSize, options.AsyncIO, options.WriteThrough)
            {
            }

            internal bool IsAsynchronous => HasFlag(options, FileOptions.Asynchronous);

            public FileStream Invoke()
            {
                var path = temporary ? Path.Combine(this.path, Path.GetRandomFileName()) : this.path;
                return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, BufferSize, options);
            }
        }

        /// <summary>
        /// Represents construction options of the writer.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Options // TODO: Must be readonly struct in .NET 6
        {
            internal const int DefaultMemoryThreshold = 32768;
            internal const int DefaultFileBufferSize = 4096;
            private int memoryThreshold, initialCapacity;
            private string? path;
            private bool keepFileAlive;
            private int? fileBufferSize;
            private bool synchronousIO;

            /// <summary>
            /// The maximum amount of memory in bytes to allocate before switching to a file on disk.
            /// </summary>
            public int MemoryThreshold
            {
                readonly get => memoryThreshold == 0 ? DefaultMemoryThreshold : memoryThreshold;
                set => memoryThreshold = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
            }

            /// <summary>
            /// Initial capacity of internal buffer. Should not be greater than <see cref="MemoryThreshold"/>.
            /// </summary>
            public int InitialCapacity
            {
                readonly get => initialCapacity;
                set => initialCapacity = value >= 0 && value < MemoryThreshold ? value : throw new ArgumentOutOfRangeException(nameof(value));
            }

            /// <summary>
            /// Gets or sets the allocator of internal buffer.
            /// </summary>
            public MemoryAllocator<byte>? MemoryAllocator
            {
                readonly get;
                set;
            }

            /// <summary>
            /// Gets or sets memory buffer for file I/O operations.
            /// </summary>
            /// <remarks>
            /// This property has no effect if <see cref="WriteThrough"/> is <see langword="true"/>.
            /// </remarks>
            public int FileBufferSize
            {
                readonly get => fileBufferSize.GetValueOrDefault(DefaultFileBufferSize);
                set => fileBufferSize = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
            }

            /// <summary>
            /// Indicates that the system should write through any intermediate cache and go directly to disk.
            /// </summary>
            /// <remarks>
            /// The default value is <see langword="false"/>.
            /// </remarks>
            public bool WriteThrough
            {
                readonly get;
                set;
            }

            /// <summary>
            /// To enable asynchronous I/O operations.
            /// </summary>
            /// <remarks>
            /// In asynchronous mode you should use asynchronous methods for writing. Synchronous
            /// calls in this mode cause performance overhead.
            /// The default value is <see langword="true"/>.
            /// </remarks>
            public bool AsyncIO
            {
                readonly get => !synchronousIO;
                set => synchronousIO = !value;
            }

            /// <summary>
            /// Defines the path to the file to be used
            /// as backing store for the written content when in-memory buffer overlfows.
            /// </summary>
            /// <remarks>
            /// If this property is defined then the file will not be deleted automatically on <see cref="Dispose"/>
            /// call.
            /// </remarks>
            public string FileName
            {
                set
                {
                    if (string.IsNullOrEmpty(value))
                        throw new ArgumentNullException(nameof(value));

                    path = value;
                    keepFileAlive = true;
                }
            }

            /// <summary>
            /// Defines the path to the existing directory for placing temporary
            /// file to be used as backing store for the written content when in-memory buffer overflows.
            /// </summary>
            /// <remarks>
            /// If this property is defined then the file will be deleted automatically on <see cref="Dispose"/> call.
            /// </remarks>
            public string? TempDir
            {
                set
                {
                    path = value;
                    keepFileAlive = false;
                }
            }

            /// <summary>
            /// Gets a value indicating that the backing store for the writer
            /// should be represented by temporary file which will be deleted automatically.
            /// </summary>
            public readonly bool UseTemporaryFile => !keepFileAlive;

            private static string DefaultTempPath
                => Environment.GetEnvironmentVariable("ASPNETCORE_TEMP").IfNullOrEmpty(System.IO.Path.GetTempPath());

            internal readonly string Path
            {
                get
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        if (keepFileAlive)
                        {
                            Debug.Fail("Permanent file path should not be empty");
                        }
                        else
                        {
                            return DefaultTempPath;
                        }
                    }

                    return path;
                }
            }

            /// <summary>
            /// Gets or sets the counter used to report allocation of the internal buffer.
            /// </summary>
            public EventCounter? AllocationCounter
            {
                readonly get;
                set;
            }
        }
    }
}