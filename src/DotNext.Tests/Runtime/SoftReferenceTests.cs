using System.Runtime.CompilerServices;

namespace DotNext.Runtime
{
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
            var ref1 = new SoftReference<object>(new());
            var ref2 = ref1;

            Equal(ref1, ref2);
            True(ref1 == ref2);
            False(ref1 != ref2);
            Equal(ref1.GetHashCode(), ref2.GetHashCode());

            ref2 = default;
            NotEqual(ref1, ref2);
            False(ref1 == ref2);
            True(ref1 != ref2);
            NotEqual(ref1.GetHashCode(), ref2.GetHashCode());
        }

        [Fact]
        public static void ReferenceState()
        {
            var reference = new SoftReference<object>(new object());
            Equal(SoftReferenceState.Strong, reference.TargetAndState.State);

            reference.Clear();
            Equal(SoftReferenceState.Empty, reference.TargetAndState.State);

            reference = default;
            Equal(SoftReferenceState.NotAllocated, reference.TargetAndState.State);
        }

        [Fact]
        public static void VolatileAccess()
        {
            var reference = new SoftReference<object>(new object());
            Equal(SoftReferenceState.Strong, SoftReference<object>.VolatileRead(ref reference).TargetAndState.State);

            SoftReference<object>.VolatileWrite(ref reference, default);
            Equal(SoftReferenceState.NotAllocated, reference.TargetAndState.State);
        }
    }
}