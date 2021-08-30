using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct EmptyClusterConfiguration
    {
        private const byte NoConfigurationState = 0;
        private const byte ProposeConfigurationState = 1;
        private const byte ApplyConfigurationState = 2;

        internal long Fingerprint { get; init; }
        internal bool ApplyConfig { get; init; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal void Deconstruct(out long fingerprint, out bool applyConfig)
        {
            fingerprint = Fingerprint;
            applyConfig = ApplyConfig;
        }

        internal static void WriteTo(in EmptyClusterConfiguration? configuration, ref SpanWriter<byte> writer)
        {
            byte configState;
            long fingerprint;
            if (configuration.HasValue)
            {
                bool applyConfig;
                (fingerprint, applyConfig) = configuration.GetValueOrDefault();
                configState = applyConfig ? ApplyConfigurationState : ProposeConfigurationState;
            }
            else
            {
                configState = NoConfigurationState;
                fingerprint = 0L;
            }

            writer.Add(configState);
            writer.WriteInt64(configState, true);
        }

        internal static EmptyClusterConfiguration? ReadFrom(ref SpanReader<byte> reader)
        {
            var configState = reader.Read();
            var fingerprint = reader.ReadInt64(true);

            return configState switch
            {
                ApplyConfigurationState => new() { Fingerprint = fingerprint, ApplyConfig = true },
                ProposeConfigurationState => new() { Fingerprint = fingerprint, ApplyConfig = false },
                _ => null,
            };
        }
    }
}