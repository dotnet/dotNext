using System;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using IO;
    using IClusterConfiguration = Membership.IClusterConfiguration;

    internal partial class ServerExchange
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct ClusterConfiguration : IClusterConfiguration
        {
            private readonly PipeReader reader;
            private readonly long fingerprint, length;

            internal ClusterConfiguration(ref ReadOnlyMemory<byte> input, PipeReader reader, out bool applyConfig)
            {
                var count = ConfigurationExchange.ParseAnnouncement(input.Span, out fingerprint, out length, out applyConfig);
                input = input.Slice(count);
                this.reader = reader;
            }

            long IClusterConfiguration.Fingerprint => fingerprint;

            long IClusterConfiguration.Length => length;

            bool IDataTransferObject.IsReusable => false;

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => length > 0L ? new(writer.CopyFromAsync(reader, token)) : IDataTransferObject.Empty.WriteToAsync(writer, token);

            ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                => IDataTransferObject.TransformAsync<TResult, TTransformation>(reader, transformation, token);
        }

        private async ValueTask<bool> BeginReceiveConfiguration(ReadOnlyMemory<byte> input, bool completed, CancellationToken token)
        {
            var config = new ClusterConfiguration(ref input, Reader, out var applyConfig);
            var result = await Writer.WriteAsync(input, token).ConfigureAwait(false);
            task = server.InstallConfigurationAsync(config, applyConfig, token);
            if (result.IsCompleted || completed)
            {
                await Writer.CompleteAsync();
                state = State.ReceivingConfigurationFinished;
            }

            return true;
        }

        private async ValueTask<bool> ReceivingConfiguration(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
        {
            if (content.IsEmpty)
            {
                completed = true;
            }
            else
            {
                var result = await Writer.WriteAsync(content, token).ConfigureAwait(false);
                completed |= result.IsCompleted;
            }

            if (completed)
            {
                await Writer.CompleteAsync().ConfigureAwait(false);
                state = State.ReceivingConfigurationFinished;
            }

            return true;
        }

        private static ValueTask<(PacketHeaders, int, bool)> RequestConfigurationChunk()
            => new((new PacketHeaders(MessageType.Continue, FlowControl.Ack), 0, true));

        private async ValueTask<(PacketHeaders, int, bool)> EndReceiveConfiguration(Memory<byte> output)
        {
            await (Interlocked.Exchange(ref task, null) ?? Task.CompletedTask).ConfigureAwait(false);
            return (new PacketHeaders(MessageType.None, FlowControl.Ack), 0, false);
        }
    }
}