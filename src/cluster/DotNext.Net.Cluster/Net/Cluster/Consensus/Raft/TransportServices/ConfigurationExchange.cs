using System.IO.Pipelines;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using IO;
using static IO.Pipelines.PipeExtensions;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal sealed class ConfigurationExchange : ClientExchange<bool>, IAsyncDisposable
{
    private readonly Pipe pipe;
    private readonly IClusterConfiguration configuration;
    private Task? transmission;

    internal ConfigurationExchange(IClusterConfiguration configuration, PipeOptions? options = null)
    {
        this.configuration = configuration;
        pipe = new Pipe(options ?? PipeOptions.Default);
    }

    private int WriteAnnouncement(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);

        writer.WriteInt64(configuration.Fingerprint, true);
        writer.WriteInt64(configuration.Length, true);

        return writer.WrittenCount;
    }

    internal static int ParseAnnouncement(ReadOnlySpan<byte> input, out long fingerprint, out long configurationLength)
    {
        var reader = new SpanReader<byte>(input);

        fingerprint = reader.ReadInt64(true);
        configurationLength = reader.ReadInt64(true);

        return reader.ConsumedCount;
    }

    private static async Task WriteConfigurationAsync(IDataTransferObject configuration, PipeWriter writer, CancellationToken token)
    {
        await configuration.WriteToAsync(writer, token).ConfigureAwait(false);
        await writer.CompleteAsync().ConfigureAwait(false);
    }

    public async override ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
    {
        var count = default(int);
        FlowControl control;

        if (transmission is null)
        {
            count = WriteAnnouncement(payload.Span);
            payload = payload.Slice(count);
            control = FlowControl.StreamStart;
            transmission = WriteConfigurationAsync(configuration, pipe.Writer, token);
        }
        else
        {
            control = FlowControl.Fragment;
        }

        count += await pipe.Reader.CopyToAsync(payload, token).ConfigureAwait(false);
        if (count < payload.Length)
            control = FlowControl.StreamEnd;
        return (new PacketHeaders(MessageType.Configuration, control), count, true);
    }

    public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        ValueTask<bool> result;

        if (transmission is { IsFaulted: true })
        {
            result = ValueTask.FromException<bool>(transmission.Exception!);
        }
        else if (headers.Type is MessageType.Continue)
        {
            result = new(true);
        }
        else
        {
            TrySetResult(true);
            result = new(false);
        }

        return result;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        var e = new ObjectDisposedException(GetType().Name);
        await pipe.Writer.CompleteAsync(e).ConfigureAwait(false);
        await pipe.Reader.CompleteAsync(e).ConfigureAwait(false);
    }
}