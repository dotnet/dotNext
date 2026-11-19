using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

using Diagnostics;
using Patterns;

public partial class LeaseProvider<TMetadata>
{
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Observation(in LeaseIdentity Identity, Timestamp Timestamp)
    {
        public Observation(in LeaseIdentity identity, TimeProvider provider)
            : this(in identity, new Timestamp(provider))
        {
        }
    }

    private interface ITransitionCondition
    {
        bool Invoke(in State state, bool isExpired);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct AcquisitionCondition(Guid owner) : ITransitionCondition
    {
        bool ITransitionCondition.Invoke(in State state, bool isExpired)
            => isExpired || state.IsOwnedBy(owner);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct RenewalCondition(Guid owner, bool reacquire) : ITransitionCondition
    {
        bool ITransitionCondition.Invoke(in State state, bool isExpired)
            => state.IsOwnedBy(owner) && (reacquire || !isExpired);
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