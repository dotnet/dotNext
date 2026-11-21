using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

using Patterns;

partial class LeaseEngine<TMetadata>
{
    private interface ITransitionCondition
    {
        bool Invoke(in LeaseState<TMetadata> state, bool isExpired);
        
        Guid OwnerId { get; }
        
        static abstract bool RequiresVersionBump { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct AcquisitionCondition(Guid ownerId) : ITransitionCondition
    {
        bool ITransitionCondition.Invoke(in LeaseState<TMetadata> state, bool isExpired)
            => isExpired || state.IsOwnedBy(ownerId);

        Guid ITransitionCondition.OwnerId => ownerId;
        
        static bool ITransitionCondition.RequiresVersionBump => true;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct RenewalCondition(Guid ownerId, bool reacquire) : ITransitionCondition
    {
        bool ITransitionCondition.Invoke(in LeaseState<TMetadata> state, bool isExpired)
            => state.IsOwnedBy(ownerId) && (reacquire || !isExpired);
        
        Guid ITransitionCondition.OwnerId => ownerId;
        
        static bool ITransitionCondition.RequiresVersionBump => false;
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
    
    // Access to the state of the particular lease in the underlying storage, and to its local observation
    internal interface ILeaseStorage
    {
        ValueTask<LeaseState<TMetadata>> GetStateAsync(CancellationToken token);

        ValueTask<bool> TryUpdateStateAsync(LeaseState<TMetadata> expected, LeaseState<TMetadata> update, CancellationToken token);
        
        // Records the observation of the version written by this provider
        Observation Observation { set; }

        // Returns zero for the released lease; otherwise, the remaining time since the first observation of the version.
        // A concurrent observation must not be overwritten. If it has the same version and stamp, reuse it: the timestamp
        // of any observation never precedes the write of the observed version and stamp. Otherwise, use the local one.
        TimeSpan GetRemainingTime(in LeaseState<TMetadata> state, TimeProvider provider);
    
        /// <summary>
        /// Gets the name of the implementation.
        /// </summary>
        string Name { get; }
    }
}

internal static class LeaseEngine
{
    internal static ArgumentException? CheckArguments(Guid ownerId, [CallerArgumentExpression(nameof(ownerId))] string paramName = "")
        => ownerId == Guid.Empty ? new(ExceptionMessages.EmptyLeaseOwner, paramName) : null;

    internal static ArgumentException? CheckArguments(Guid ownerId, Delegate? updater,
        [CallerArgumentExpression(nameof(ownerId))]
        string idParamName = "",
        [CallerArgumentExpression(nameof(updater))]
        string updaterParamName = "")
        => CheckArguments(ownerId, idParamName) ?? (updater is null ? new ArgumentNullException(updaterParamName) : null);
}