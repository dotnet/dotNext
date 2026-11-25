using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime;

/// <summary>
/// Represents a form of weak reference
/// which is eligible for garbage collection in Generation 2 only.
/// </summary>
/// <remarks>
/// The object that is soft-referenced survives the garbage collection in
/// Generation 0 and 1. When the object reaches Generation 2, the soft reference
/// downgrades to a weak reference according to <see cref="SoftReferenceOptions"/>,
/// so the object can be reclaimed by the subsequent GC in Generation 2.
/// All public instance members of this type are thread-safe.
/// </remarks>
/// <typeparam name="T">The type of the object referenced.</typeparam>
[DebuggerDisplay($"State = {{{nameof(State)}}}")]
public sealed partial class SoftReference<T> : IOptionMonad<T>
    where T : class
{
    // Thread safety: this field must be requested first. If it is null then try to get the reference
    // from trackerRef
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private volatile T? strongRef;

    // Target can be of type Tracker, or of type T
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private volatile GCIntermediateReference? trackerRef;

    /// <summary>
    /// Initializes a new soft reference.
    /// </summary>
    /// <param name="target">The object to be referenced.</param>
    /// <param name="options">The behavior of soft reference.</param>
    public SoftReference(T? target, SoftReferenceOptions? options = null)
    {
        if (target is not null)
        {
            options ??= SoftReferenceOptions.Default;
            var gen2CollectionCount = SoftReferenceOptions.UnknownCollectionCount;

            // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/8.0/getgeneration-return-value
            if (GC.GetGeneration(target) is int.MaxValue)
            {
                strongRef = target;
            }
            else if (options.KeepTracking(target, ref gen2CollectionCount))
            {
                strongRef = target;
                trackerRef = new(new Tracker(this, options) { Gen2CollectionCount = gen2CollectionCount });
                return;
            }
            else
            {
                trackerRef = new(target);
            }
        }

        // the finalizer is needed only to stop the tracker
        GC.SuppressFinalize(this);
    }

    private void ClearCore()
    {
        Interlocked.Exchange(ref trackerRef, null)?.Clear();
        strongRef = null;
    }

    /// <summary>
    /// Makes the referenced object available for garbage collection (if not referenced elsewhere).
    /// </summary>
    /// <remarks>
    /// This method stops tracking the referenced object. Thus, the object
    /// will be reclaimable by GC even if it is not reached Generation 2.
    /// </remarks>
    public void Clear()
    {
        ClearCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tries to retrieve the target object.
    /// </summary>
    /// <param name="target">The referenced object.</param>
    /// <returns><see langword="true"/> if the target was retrieved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTarget([NotNullWhen(true)] out T? target)
        => (target = Target) is not null;

    /// <summary>
    /// Tries to retrieve the target object.
    /// </summary>
    /// <returns>The referenced object.</returns>
    public Optional<T> TryGetTarget() => new(Target);

    /// <summary>
    /// Gets state of the referenced object and referenced object itself.
    /// </summary>
    /// <remarks>
    /// The returned target object is not <see langword="null"/> when the state is <see cref="SoftReferenceState.Strong"/>
    /// or <see cref="SoftReferenceState.Weak"/>.
    /// </remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public (T? Target, SoftReferenceState State) TargetAndState
    {
        get
        {
            SoftReferenceState state;

            if (strongRef is { } target)
            {
                state = SoftReferenceState.Strong;
            }
            else
            {
                target = trackerRef?.Target as T;
                state = target is null ? SoftReferenceState.Empty : SoftReferenceState.Weak;
            }

            return (target, state);
        }
    }

    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private SoftReferenceState State => TargetAndState.State;

    private T? Target => strongRef ?? trackerRef?.Target as T;

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool IOptionMonad<T>.HasValue => Target is not null;

    /// <inheritdoc />
    [return: NotNullIfNotNull(nameof(defaultValue))]
    T? IOptionMonad<T>.Or(T? defaultValue) => Target ?? defaultValue;

    /// <inheritdoc />
    T? IOptionMonad<T>.ValueOrDefault => Target;

    /// <inheritdoc />
    T IOptionMonad<T>.OrInvoke(Func<T> defaultFunc) => Target ?? defaultFunc();

    /// <inheritdoc />
    bool IOptionMonad<T>.TryGet([NotNullWhen(true)] out T? target) => TryGetTarget(out target);

    /// <inheritdoc />
    object? ISupplier<object?>.Invoke() => Target;

    /// <inheritdoc />
    public override string? ToString() => Target?.ToString();

    /// <summary>
    /// Gets the referenced object.
    /// </summary>
    /// <param name="reference">The reference to the object.</param>
    /// <returns>The referenced object; or <see langword="null"/> if the object is not reachable.</returns>
    public static explicit operator T?(SoftReference<T>? reference) => reference?.Target;

    /// <summary>
    /// Tries to retrieve the target object.
    /// </summary>
    /// <param name="reference">Soft reference.</param>
    /// <returns>The referenced object.</returns>
    public static explicit operator Optional<T>(SoftReference<T>? reference)
        => reference?.TryGetTarget() ?? Optional<T>.None;

    /// <summary>
    /// Makes the referenced object available for garbage collection (if not referenced elsewhere).
    /// </summary>
    ~SoftReference() => ClearCore(); // if SoftRef itself is not reachable then prevent prolongation of the referenced object lifetime
}

/// <summary>
/// Allows to configure behavior of <see cref="SoftReference{T}"/>.
/// </summary>
public class SoftReferenceOptions
{
    internal const int UnknownCollectionCount = -1;
    private readonly int collectionCount;
    private readonly long memoryLimit = long.MaxValue;
    private readonly double memoryThreshold = 1D;

    /// <summary>
    /// Gets default options.
    /// </summary>
    public static SoftReferenceOptions Default { get; } = new();

    /// <summary>
    /// Indicates how many Generation 2 collections can the referenced object survive
    /// after it has been promoted to Generation 2.
    /// </summary>
    /// <remarks>
    /// The default is 0.
    /// </remarks>
    /// <value>The number of collections; must not be negative.</value>
    public int CollectionCount
    {
        get => collectionCount;
        init => collectionCount = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets the memory limit for Gen2 used as a trigger to release a reference to the object.
    /// </summary>
    /// <remarks>
    /// If soft reference detects that Gen2 occupies more memory than the limit then the referenced
    /// object will be marked as available for garbage collection regardless of <see cref="CollectionCount"/>
    /// configuration value.
    /// </remarks>
    public long MemoryLimit
    {
        get => memoryLimit;
        init => memoryLimit = value > 0L ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets the memory threshold.
    /// </summary>
    /// <remarks>
    /// The default is 1.
    /// </remarks>
    /// <value>The memory threshold; must be in range (0, 1].</value>
    public double MemoryThreshold
    {
        get => memoryThreshold;
        init => memoryThreshold = double.IsFinite(value) && value is > 0D and <= 1D ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    // gen2CollectionCount is the number of Gen2 collections when the target has been observed in Gen2 for the first time
    internal bool KeepTracking(object target, ref int gen2CollectionCount)
    {
        var generation = GC.GetGeneration(target);
        if (generation < GC.MaxGeneration)
            return true;

        var currentCount = GC.CollectionCount(generation);
        if (gen2CollectionCount is UnknownCollectionCount)
            gen2CollectionCount = currentCount;

        return currentCount - gen2CollectionCount <= collectionCount && CheckLimits(generation);
    }

    private bool CheckLimits(int generation)
    {
        var info = GC.GetGCMemoryInfo();
        return info.Index is 0L || CheckMemoryLimit(generation, in info) && CheckMemoryThreshold(in info);
    }

    private bool CheckMemoryThreshold(in GCMemoryInfo info)
        => info.MemoryLoadBytes <= info.HighMemoryLoadThresholdBytes * memoryThreshold;

    private bool CheckMemoryLimit(int generation, in GCMemoryInfo info)
    {
        ReadOnlySpan<GCGenerationInfo> generations;

        return memoryLimit is long.MaxValue
            || (generations = info.GenerationInfo).Length <= generation
            || generations[generation].SizeAfterBytes < memoryLimit;
    }
}

/// <summary>
/// Represents state of the referenced object.
/// </summary>
public enum SoftReferenceState
{
    /// <summary>
    /// The referenced object is not reachable via soft reference.
    /// </summary>
    Empty = 0,

    /// <summary>
    /// Soft reference acting as a weak reference so the referenced object is available for GC.
    /// </summary>
    Weak,

    /// <summary>
    /// Soft reference acting as a strong reference so the referenced object is not available for GC.
    /// </summary>
    Strong,
}