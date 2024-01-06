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

        for (var i = 0; i < 30; i++)
        {
            new object();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

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
        Equal(string.Empty, monad.OrInvoke(Func.Constant(string.Empty)));
        Null(monad.ValueOrDefault);
        Equal(string.Empty, monad.Or(string.Empty));

        monad = new SoftReference<object>(new());
        True(monad.HasValue);
        True(monad.TryGet(out var target));
        Same(monad.ValueOrDefault, target);
        Same(target, monad.Or(string.Empty));
        Same(target, monad.OrInvoke(Func.Constant(string.Empty)));
    }
}