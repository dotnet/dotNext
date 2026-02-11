using System.Collections.Immutable;

namespace DotNext.Collections.Generic;

public sealed class SetTests : Test
{
    [Fact]
    public static void SingletonSet()
    {
        var set = IReadOnlySet<int>.Singleton(10);

        // contains
        True(set.Contains(10));
        False(set.Contains(20));
        Contains(10, set);
        DoesNotContain(20, set);

        True(set.IsSupersetOf(ImmutableHashSet<int>.Empty));
        True(set.IsProperSupersetOf(ImmutableHashSet<int>.Empty));

        var superset = new[] { 10, 20 };
        True(set.IsSubsetOf(superset));
        True(set.IsProperSubsetOf(superset));

        True(set.Overlaps(superset));
        False(set.Overlaps([30, 40]));

        True(set.SetEquals(ImmutableHashSet.Create(10)));
        False(set.SetEquals(ImmutableHashSet.Create([10, 20])));
    }

    [Fact]
    public static void EmptySet()
    {
        var set = IReadOnlySet<long>.Range(0L.Disclosed, 1L.Disclosed);
        Empty(set);
        Same(IReadOnlySet<long>.Empty, set);
    }

    [Fact]
    public static void SingletonSet2()
    {
        var set = IReadOnlySet<long>.Range(0L.Enclosed, 1L.Disclosed);
        NotEmpty(set);
        Single(set, 0L);
    }

    [Fact]
    public static void SetEquals()
    {
        var expected = IReadOnlySet<long>.Range(0L.Enclosed, 3L.Disclosed);
        True(expected.Contains(0L));
        True(expected.Contains(1L));
        True(expected.Contains(2L));
        False(expected.Contains(3L));
        Equal(3, expected.Count);

        var actual = ImmutableHashSet.CreateRange(expected);
        True(expected.SetEquals(actual));
    }

    [Fact]
    public static void SetOperations()
    {
        var set = IReadOnlySet<long>.Range(0L.Enclosed, 3L.Disclosed);

        True(set.IsSupersetOf(ImmutableHashSet<long>.Empty));
        True(set.IsProperSupersetOf(ImmutableHashSet<long>.Empty));

        True(set.Overlaps(ImmutableHashSet.Create([-1L, 0L])));

        False(set.IsProperSubsetOf(ImmutableHashSet.Create([-1L, 0L])));
        True(set.IsSubsetOf(ImmutableHashSet.Create([0L, 1L, 2L, 3L])));
        True(set.IsProperSubsetOf(ImmutableHashSet.Create([0L, 1L, 2L, 3L])));
    }
}