using System.Runtime.InteropServices;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;

/// <summary>
/// Represents persistent cluster configuration storage.
/// </summary>
/// <typeparam name="TAddress">The type of the cluster member address.</typeparam>
public abstract class PersistentClusterConfigurationStorage<TAddress> : ClusterConfigurationStorage<TAddress>
    where TAddress : notnull
{
    private const FileOptions Options = FileOptions.Asynchronous | FileOptions.SequentialScan;
    
    private readonly string configurationFile;
    private readonly nint openFileFunction;

    /// <summary>
    /// Initializes a new persistent storage.
    /// </summary>
    /// <param name="fileName">The full path to the file used as persistent storage of cluster members.</param>
    protected PersistentClusterConfigurationStorage(string fileName)
    {
        configurationFile = fileName;

        if (OperatingSystem.IsLinux())
        {
            NativeLibrary.TryGetExport(NativeLibrary.GetMainProgramHandle(), "open", out openFileFunction);
        }
    }

    /// <inheritdoc/>
    protected sealed override async ValueTask<(MemoryOwner<byte> Configuration, long Version)> LoadConfigurationAsync(CancellationToken token)
    {
        if (!File.Exists(configurationFile))
            return default;

        using var handle = File.OpenHandle(configurationFile, FileMode.Open, FileAccess.Read, FileShare.Read, Options);

        var length = int.CreateChecked(RandomAccess.GetLength(handle));
        var versionBuffer = MemoryAllocator.AllocateExactly(sizeof(long));
        try
        {
            var configBuffer = MemoryAllocator.AllocateExactly(length - sizeof(long));
            await RandomAccess.ReadAsync(handle, [versionBuffer.Memory, configBuffer.Memory], fileOffset: 0L, token).ConfigureAwait(false);
            return (configBuffer, ReadInt64LittleEndian(versionBuffer.Span));
        }
        finally
        {
            versionBuffer.Dispose();
        }
    }

    /// <inheritdoc/>
    protected sealed override ValueTask<bool> SaveConfigurationAsync(ReadOnlyMemory<byte> configuration, long configurationVersion,
        CancellationToken token)
        => File.Exists(configurationFile)
            ? RewriteConfigurationAsync(configuration, configurationVersion, token)
            : SaveFreshConfigurationAsync(configuration, configurationVersion, token);

    private async ValueTask<bool> SaveFreshConfigurationAsync(ReadOnlyMemory<byte> configuration, long configurationVersion,
        CancellationToken token)
    {
        var versionBuffer = MemoryAllocator.AllocateExactly(sizeof(long));
        var handle = File.OpenHandle(configurationFile,
            FileMode.CreateNew,
            FileAccess.Write, FileShare.None,
            Options,
            configuration.Length + sizeof(long));

        try
        {
            WriteInt64LittleEndian(versionBuffer.Span, configurationVersion);
            await RandomAccess
                .WriteAsync(handle, [versionBuffer.Memory, configuration], fileOffset: 0L, token)
                .ConfigureAwait(false);

            RandomAccess.FlushToDisk(handle);
        }
        finally
        {
            versionBuffer.Dispose();
            handle.Dispose();
        }
        
        return true;
    }

    private async ValueTask<bool> RewriteConfigurationAsync(ReadOnlyMemory<byte> configuration, long configurationVersion,
        CancellationToken token)
    {
        var versionBuffer = MemoryAllocator.AllocateExactly(sizeof(long));
        try
        {
            long version;

            // restore version from file
            using (var handle = File.OpenHandle(configurationFile, FileMode.Open, FileAccess.Read, FileShare.Read, Options))
            {
                await RandomAccess.ReadAsync(handle, versionBuffer.Memory, fileOffset: 0L, token).ConfigureAwait(false);
                version = ReadInt64LittleEndian(versionBuffer.Span);
            }

            if (configurationVersion <= version)
                return false;

            // rewrite config
            var tempFile = Path.Combine(Path.GetDirectoryName(configurationFile.AsSpan()).ToString(),
                string.Concat(Path.GetRandomFileName(), ".tmp"));
            using (var handle = File.OpenHandle(tempFile,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       Options,
                       configuration.Length + sizeof(long)))
            {
                File.SetAttributes(handle, FileAttributes.NotContentIndexed);
                WriteInt64LittleEndian(versionBuffer.Span, configurationVersion);
                await RandomAccess
                    .WriteAsync(handle, [versionBuffer.Memory, configuration], fileOffset: 0L, token)
                    .ConfigureAwait(false);
                RandomAccess.FlushToDisk(handle);
            }

            Move(tempFile, configurationFile);
        }
        finally
        {
            versionBuffer.Dispose();
        }

        return true;
    }

    private unsafe void Move(string sourceFileName, string destFileName)
    {
        File.Move(sourceFileName, destFileName, overwrite: true);

        if (OperatingSystem.IsLinux() && openFileFunction is not 0)
        {
            Directory.FlushToDisk(Path.GetDirectoryName(destFileName.AsSpan()),
                (delegate*unmanaged<byte*, int, int, int>)openFileFunction);
        }
    }
}