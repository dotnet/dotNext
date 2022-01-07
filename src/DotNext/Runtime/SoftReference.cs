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
public readonly struct SoftReference<T> : IEquatable<SoftReference<T>>, IOptionMonad<T>
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

    private sealed class TrackerReference : WeakReference
    {
        private bool cleared;

        internal TrackerReference(T target, SoftReferenceOptions options)
            : base(null, trackResurrection: true)
        {
            var tracker = new Tracker(target, this, options);
            Target = tracker;
            GC.KeepAlive(tracker);
        }

        internal TrackerReference(T target)
            : base(target, trackResurrection: false)
        {
        }

        internal void Clear()
        {
            (Target as Tracker)?.StopTracking();
            Target = null;
            cleared = true;
        }

        ~TrackerReference()
        {
            if (!cleared)
                Clear();
        }
    }

    // Target can be null, of type Tracker, or of type T
    private readonly TrackerReference? trackerRef;

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

    private SoftReference(TrackerReference? trackerRef) => this.trackerRef = trackerRef;

    /// <summary>
    /// Makes the referenced object available for garbage collection (if not referenced elsewhere).
    /// </summary>
    /// <remarks>
    /// This method stops tracking the referenced object. Thus, the object
    /// will be reclaimable by GC even if it is not reached Generation 2.
    /// </remarks>
    public void Clear() => trackerRef?.Clear();

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

    /// <summary>
    /// Determines whether this reference is the same as the specified reference.
    /// </summary>
    /// <param name="other">Soft reference to compare.</param>
    /// <returns><see langword="true"/> if this reference is the same as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(SoftReference<T> other) => ReferenceEquals(trackerRef, other.trackerRef);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? other)
        => other is SoftReference<T> reference && Equals(reference);

    /// <inheritdoc />
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(trackerRef);

    /// <inheritdoc />
    public override string? ToString() => Target?.ToString();

    /// <summary>
    /// Determines whether the two objects point to the same target.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both objects point to the same target; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(SoftReference<T> x, SoftReference<T> y)
        => x.trackerRef == y.trackerRef;

    /// <summary>
    /// Determines whether the two objects point to different targets.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both objects point to different targets; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(SoftReference<T> x, SoftReference<T> y)
        => x.trackerRef != y.trackerRef;

    /// <summary>
    /// Gets the referenced object.
    /// </summary>
    /// <param name="reference">The reference to the object.</param>
    /// <returns>The referenced object; or <see langword="null"/> if the object is not reachable.</returns>
    public static explicit operator T?(SoftReference<T> reference) => reference.Target;

    /// <summary>
    /// Tries to retrieve the target object.
    /// </summary>
    /// <param name="reference">Soft reference.</param>
    /// <returns>
    /// The referenced object;
    /// or <see cref="Optional{T}.None"/> if reference is not allocated;
    /// or <see cref="Optional{T}.IsNull"/> is <see langword="true"/>.
    /// </returns>
    public static explicit operator Optional<T>(SoftReference<T> reference)
        => reference.TryGetTarget();

    /// <summary>
    /// Reads soft reference and prevents the processor from reordering memory operations.
    /// </summary>
    /// <param name="location">The managed pointer to soft reference.</param>
    /// <returns>The value at the specified location.</returns>
    public static SoftReference<T> VolatileRead(ref SoftReference<T> location)
        => new(Volatile.Read(ref Unsafe.AsRef(in location.trackerRef)));

    /// <summary>
    /// Writes soft reference and prevents the proces from reordering memory operations.
    /// </summary>
    /// <param name="location">The managed pointer to soft reference.</param>
    /// <param name="value">The value to write.</param>
    public static void VolatileWrite(ref SoftReference<T> location, SoftReference<T> value)
        => Volatile.Write(ref Unsafe.AsRef(location.trackerRef), value.trackerRef);

    /// <summary>
    /// Sets soft reference to a specified value and
    /// returns the original value, as an atomic operation.
    /// </summary>
    /// <param name="location">The location of the soft reference to modify.</param>
    /// <param name="value">The value to which the <paramref name="location"/> parameter is set.</param>
    /// <returns>The original value at <paramref name="location"/>.</returns>
    public static SoftReference<T> Exchange(ref SoftReference<T> location, SoftReference<T> value)
        => new(Interlocked.Exchange(ref Unsafe.AsRef(in location.trackerRef), value.trackerRef));

    /// <summary>
    /// Compares two soft references for equality and,
    /// if they are equal, replaces the first one.
    /// </summary>
    /// <param name="location">The location of the soft reference to modify.</param>
    /// <param name="value">The value to which the <paramref name="location"/> parameter is set.</param>
    /// <param name="comparand">The reference that is compared by reference to the value at <paramref name="location"/>.</param>
    /// <returns>The original reference at <paramref name="location"/>.</returns>
    public static SoftReference<T> CompareExchange(ref SoftReference<T> location, SoftReference<T> value, SoftReference<T> comparand)
        => new(Interlocked.CompareExchange(ref Unsafe.AsRef(in location.trackerRef), value.trackerRef, comparand.trackerRef));
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