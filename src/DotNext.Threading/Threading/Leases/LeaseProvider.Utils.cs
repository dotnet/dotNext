using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

using Diagnostics;
using Patterns;

public partial class LeaseProvider<TMetadata>
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct Observation(in LeaseIdentity identity, Timestamp timestamp)
    {
        public Observation(in LeaseIdentity identity, TimeProvider provider)
            : this(in identity, new Timestamp(provider))
        {
        }

        public readonly LeaseIdentity Identity = identity;
        public readonly Timestamp Timestamp = timestamp;

        public bool IsEmpty => Timestamp.IsEmpty;
    }
    
    private interface ITransitionCondition
    {
        bool Invoke(in State state, bool isExpired);
    }

    private sealed class AcquisitionCondition : ITransitionCondition, ISingleton<AcquisitionCondition>
    {
        public static AcquisitionCondition Instance { get; } = new();

        private AcquisitionCondition()
        {
        }

        bool ITransitionCondition.Invoke(in State state, bool isExpired) => isExpired;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct RenewalCondition(LeaseIdentity identity, bool reacquire) : ITransitionCondition
    {
        bool ITransitionCondition.Invoke(in State state, bool isExpired)
            => identity.Version is not LeaseIdentity.InitialVersion
               && state.Identity == identity
               && (reacquire || !isExpired);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct AcquisitionOrRenewalCondition(LeaseIdentity identity) : ITransitionCondition
    {
        bool ITransitionCondition.Invoke(in State state, bool isExpired)
            => state.Identity == identity || isExpired;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct Updater<TArg>(TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater) : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        ValueTask<TMetadata> ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>.Invoke(TMetadata metadata, CancellationToken token)
            => updater(arg, metadata, token);
    }

    private sealed class NoOpUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>, ISingleton<NoOpUpdater>
    {
        public static NoOpUpdater Instance { get; } = new();

        private NoOpUpdater()
        {
        }

        ValueTask<TMetadata> ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>.Invoke(TMetadata metadata, CancellationToken token)
            => ValueTask.FromResult(metadata);
    }
}