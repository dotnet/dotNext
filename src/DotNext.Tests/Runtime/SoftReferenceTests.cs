using System.Runtime.CompilerServices;

namespace DotNext.Runtime;

public sealed class SoftReferenceTests : Test
{
    private sealed class Target
    {
        internal bool IsAlive = true;

        ~Target() => IsAlive = false;
    }

    [Fact]
    public static void SurviveGen0GC()
    {
        var reference = CreateReference();

        for (var i = 0; i < 30; i++)
        {
            new object();
            GC.Collect(generation: 0);
            True(IsAlive(reference));
        }

        True(reference.TargetAndState.Target.IsAlive);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static SoftReference<Target> CreateReference() => new(new());

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool IsAlive(SoftReference<Target> r) => r.TryGetTarget(out _);
    }

    [Fact]
    public static void WithOptions()
    {
        var reference = CreateReference();

        do
        {
            GC.KeepAlive(new());
            GC.Collect();
            GC.WaitForPendingFinalizers();
        } while (reference.As<IOptionMonad<Target>>().HasValue);

        Null((Target)reference);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static SoftReference<Target> CreateReference()
            => new(new(), new SoftReferenceOptions { CollectionCount = int.MaxValue, MemoryLimit = 1 });
    }

    [Fact]
    public static void TrackStrongReference()
    {
        var expected = new object();
        var reference = new SoftReference<object>(expected);

        for (var i = 0; i < 30; i++)
        {
            new object();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        var (actual, state) = reference.TargetAndState;
        Same(expected, actual);
        Equal(SoftReferenceState.Weak, state);

        GC.KeepAlive(expected);
    }

    [Fact]
    public static void Operators()
    {
        var reference = new SoftReference<string>(string.Empty);
        Same(reference.TargetAndState.Target, ((Optional<string>)reference).Value);
        Same(reference.TargetAndState.Target, (string)reference);

        reference.Clear();
        True(((Optional<string>)reference).IsNull);
        Null((string)reference);
    }

    [Fact]
    public static void ReferenceState()
    {
        var reference = new SoftReference<object>(new object());
        Equal(SoftReferenceState.Strong, reference.TargetAndState.State);

        reference.Clear();
        Equal(SoftReferenceState.Empty, reference.TargetAndState.State);
    }

    [Fact]
    public static void OptionMonadInterfaceInterop()
    {
        IOptionMonad<object> monad = new SoftReference<object>(null);
        False(monad.HasValue);
        False(monad.TryGet(out _));
        Equal(string.Empty, monad.OrInvoke(Func<string>.Constant(string.Empty)));
        Null(monad.ValueOrDefault);
        Equal(string.Empty, monad.Or(string.Empty));

        monad = new SoftReference<object>(new());
        True(monad.HasValue);
        True(monad.TryGet(out var target));
        Same(monad.ValueOrDefault, target);
        Same(target, monad.Or(string.Empty));
        Same(target, monad.OrInvoke(Func<string>.Constant(string.Empty)));
    }

    [Fact]
    public static void CollectionCountIsRelativeToPromotion()
    {
        // make sure that the process-wide number of Gen2 collections exceeds CollectionCount
        const int collectionCount = 3;
        for (var i = 0; i <= collectionCount; i++)
            FullCollection();

        var reference = CreateReference();

        // promote the target to Gen2
        while (reference.TargetAndState is (Target: { } target, State: SoftReferenceState.Strong) && GC.GetGeneration(target) < GC.MaxGeneration)
            FullCollection();

        for (var i = 0; i < collectionCount; i++)
        {
            FullCollection();
            Equal(SoftReferenceState.Strong, reference.TargetAndState.State);
        }

        FullCollection();
        Equal(SoftReferenceState.Weak, reference.TargetAndState.State);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static SoftReference<object> CreateReference()
            => new(new(), new SoftReferenceOptions { CollectionCount = collectionCount });
    }

    private sealed class FinalizableTarget
    {
        internal static volatile bool Finalized;
        internal bool IsAlive = true;

        ~FinalizableTarget()
        {
            IsAlive = false;
            Finalized = true;
        }
    }

    [Fact]
    public static void FinalizedTargetIsNotObservable()
    {
        var reference = CreateReference();

        // wait for downgrade to weak reference
        while (GetState(reference) is SoftReferenceState.Strong)
            FullCollection();

        // the target is not reachable, so it must be finalized
        for (var i = 0; i < 10 && !FinalizableTarget.Finalized; i++)
            FullCollection();

        True(FinalizableTarget.Finalized);
        False(IsFinalizedTargetObservable(reference));

        [MethodImpl(MethodImplOptions.NoInlining)]
        static SoftReference<FinalizableTarget> CreateReference() => new(new());

        [MethodImpl(MethodImplOptions.NoInlining)]
        static SoftReferenceState GetState(SoftReference<FinalizableTarget> reference) => reference.TargetAndState.State;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool IsFinalizedTargetObservable(SoftReference<FinalizableTarget> reference)
            => reference.TryGetTarget(out var target) && !target.IsAlive;
    }

    [Fact]
    public static void DefaultCollectionCount()
    {
        Equal(0, SoftReferenceOptions.Default.CollectionCount);
        Equal(0, new SoftReferenceOptions { CollectionCount = 0 }.CollectionCount);
        Throws<ArgumentOutOfRangeException>(static () => new SoftReferenceOptions { CollectionCount = -1 });
    }

    private static void FullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}