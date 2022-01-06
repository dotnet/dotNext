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
public readonly struct SoftReference<T> : IEquatable<SoftReference<T>>, ISupplier<T?>
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
    T? ISupplier<T?>.Invoke() => Target;

    /// <summary>
    /// Determines whether this object points to the same target as the speicifed object.
    /// </summary>
    /// <param name="other">Soft reference to compare.</param>
    /// <returns><see langword="true"/> if this object points to the same target as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(SoftReference<T> other) => ReferenceEquals(Target, other.Target);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? other)
        => other is SoftReference<T> reference ? Equals(reference) : ReferenceEquals(Target, other);

    /// <inheritdoc />
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Target);

    /// <inheritdoc />
    public override string? ToString() => Target?.ToString();

    /// <summary>
    /// Determines whether the two objects point to the same target.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both objects point to the same target; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(SoftReference<T> x, SoftReference<T> y)
        => x.Target == y.Target;

    /// <summary>
    /// Determines whether the two objects point to different targets.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both objects point to different targets; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(SoftReference<T> x, SoftReference<T> y)
        => x.Target != y.Target;

    /// <summary>
    /// Gets the referenced object.
    /// </summary>
    /// <param name="reference">The reference to the object.</param>
    /// <returns>The referenced object; or <see langword="null"/> if the object is not reachable.</returns>
    public static explicit operator T?(SoftReference<T> reference) => reference.Target;

    /// <summary>
    /// Casts typed reference to a reference of type <see cref="object"/>.
    /// </summary>
    /// <param name="reference">The reference to cast.</param>
    public static implicit operator SoftReference<object>(SoftReference<T> reference)
        => new(reference.trackerRef);
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

        return info.Index is 0
            || (generations = info.GenerationInfo).Length <= generation
            || generations[generation].SizeAfterBytes < memoryLimit;
    }
}