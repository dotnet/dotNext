using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime;

/// <summary>
/// Represents a form of weak reference
/// which is eligible for garbage collection in Generation 2 only.
/// </summary>
/// <remarks>
/// The object that is soft-referenced survive garbage collection in
/// Generation 0 and 1. When GC is triggered for Generation 2,
/// the object can be reclaimed according to <see cref="SoftReferenceOptions"/>.
/// All public instance members of this type are thread-safe.
/// </remarks>
/// <typeparam name="T">The type of the object referenced.</typeparam>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay($"State = {{{nameof(State)}}}")]
public sealed class SoftReference<T> : IOptionMonad<T>
    where T : class
{
    // tracks generation of Target in each GC collection using Finalizer as a callback
    private sealed class Tracker
    {
        internal readonly T Target;
        private readonly SoftReferenceOptions options;
        private readonly WeakReference parent;

        internal Tracker(T target, WeakReference parent, SoftReferenceOptions options)
        {
            Target = target;
            this.options = options;
            this.parent = parent;
        }

        // true if SoftReference<T>.Clear() was not called
        private bool IsValid => ReferenceEquals(this, parent.Target);

        internal void StopTracking() => GC.SuppressFinalize(this);

        ~Tracker()
        {
            if (IsValid && options.KeepTracking(Target))
                GC.ReRegisterForFinalize(this);
            else
                parent.Target = Target; // downgrade reference from soft to weak
        }
    }

    private sealed class IntermediateReference : WeakReference
    {
        internal IntermediateReference(T target, SoftReferenceOptions options)
            : base(null, trackResurrection: true)
        {
            var tracker = new Tracker(target, this, options);
            Target = tracker;
            GC.KeepAlive(tracker);
        }

        internal IntermediateReference(T target)
            : base(target, trackResurrection: false)
        {
        }

        internal void Clear()
        {
            switch (Target)
            {
                case null:
                    break;
                case Tracker tracker:
                    tracker.StopTracking();
                    goto default;
                default:
                    // Change target only if it is alive (not null).
                    // Otherwise, CLR GC thread may crash with InvalidOperationException
                    // because underlying GC handle is no longer valid
                    Target = null;
                    break;
            }
        }
    }

    // Target can be null, of type Tracker, or of type T
    private volatile IntermediateReference? trackerRef;

    /// <summary>
    /// Initializes a new soft reference.
    /// </summary>
    /// <param name="target">The object to be referenced.</param>
    /// <param name="options">The behavior of soft reference.</param>
    public SoftReference(T? target, SoftReferenceOptions? options = null)
    {
        options ??= SoftReferenceOptions.Default;

        trackerRef = target is null
            ? null
            : options.KeepTracking(target)
            ? new(target, options)
            : new(target);
    }

    private void ClearCore() => Interlocked.Exchange(ref trackerRef, null)?.Clear();

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
    /// <returns>
    /// The referenced object;
    /// or <see cref="Optional{T}.None"/> if reference is not allocated;
    /// or <see cref="Optional{T}.IsNull"/> is <see langword="true"/>.
    /// </returns>
    public Optional<T> TryGetTarget()
    {
        var (target, state) = TargetAndState;
        return state is SoftReferenceState.NotAllocated ? Optional<T>.None : new(target);
    }

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
            var trackerRef = this.trackerRef;
            Debug.Assert(trackerRef is null or { Target: null or Tracker or T });

            return trackerRef is null
                ? (null, SoftReferenceState.NotAllocated)
                : trackerRef.Target switch
                {
                    null => (null, SoftReferenceState.Empty),
                    Tracker tracker => (tracker.Target, SoftReferenceState.Strong),
                    object target => (Unsafe.As<T>(target), SoftReferenceState.Weak)
                };
        }
    }

    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private SoftReferenceState State => TargetAndState.State;

    private T? Target
    {
        get
        {
            var target = trackerRef?.Target;
            Debug.Assert(target is null or Tracker or T);

            return target is Tracker tracker ? tracker.Target : Unsafe.As<T>(target);
        }
    }

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool IOptionMonad<T>.HasValue => Target is not null;

    /// <inheritdoc />
    [return: NotNullIfNotNull("defaultValue")]
    T? IOptionMonad<T>.Or(T? defaultValue) => Target ?? defaultValue;

    /// <inheritdoc />
    T? IOptionMonad<T>.OrDefault() => Target;

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
    /// <returns>
    /// The referenced object;
    /// or <see cref="Optional{T}.None"/> if reference is not allocated;
    /// or <see cref="Optional{T}.IsNull"/> is <see langword="true"/>.
    /// </returns>
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
    private readonly int collectionCount;
    private readonly long memoryLimit = long.MaxValue;

    /// <summary>
    /// Gets default options.
    /// </summary>
    public static SoftReferenceOptions Default { get; } = new();

    /// <summary>
    /// Indicates how many collections can the referenced object survive.
    /// </summary>
    public int CollectionCount
    {
        get => collectionCount;
        init => collectionCount = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
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

    internal bool KeepTracking(object target)
    {
        var generation = GC.GetGeneration(target);
        return generation < GC.MaxGeneration || (GC.CollectionCount(generation) <= collectionCount && (memoryLimit is long.MaxValue || CheckMemoryLimit(generation)));
    }

    private bool CheckMemoryLimit(int generation)
    {
        var info = GC.GetGCMemoryInfo();
        ReadOnlySpan<GCGenerationInfo> generations;

        return info.Index is 0L
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
    /// Soft reference is not allocated.
    /// </summary>
    NotAllocated = 0,

    /// <summary>
    /// The referenced object is not reachable via soft reference.
    /// </summary>
    Empty,

    /// <summary>
    /// Soft reference acting as a weak reference so the referenced object is available for GC.
    /// </summary>
    Weak,

    /// <summary>
    /// Soft reference acting as a strong reference so the referenced object is not available for GC.
    /// </summary>
    Strong,
}