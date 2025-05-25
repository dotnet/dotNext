using System.Collections.Immutable;

namespace DotNext.Collections.Generic;

public sealed class SetTests : Test
{
    [Fact]
    public static void SingletonSet()
    {
        var set = Set.Singleton(10);

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
        var set = Set.Range<long, DisclosedEndpoint<long>, DisclosedEndpoint<long>>(0L, 1L);
        Empty(set);
    }

    [Fact]
    public static void SingletonSet2()
    {
        var set = Set.Range<long, EnclosedEndpoint<long>, DisclosedEndpoint<long>>(0L, 1L);
        NotEmpty(set);
        Single(set, 0L);
    }

    [Fact]
    public static void SetEquals()
    {
        var expected = Set.Range<long, EnclosedEndpoint<long>, DisclosedEndpoint<long>>(0L, 3L);
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
        var set = Set.Range<long, EnclosedEndpoint<long>, DisclosedEndpoint<long>>(0L, 3L);

        True(set.IsSupersetOf(ImmutableHashSet<long>.Empty));
        True(set.IsProperSupersetOf(ImmutableHashSet<long>.Empty));

        True(set.Overlaps(ImmutableHashSet.Create([-1L, 0L])));

        False(set.IsProperSubsetOf(ImmutableHashSet.Create([-1L, 0L])));
        True(set.IsSubsetOf(ImmutableHashSet.Create([0L, 1L, 2L, 3L])));
        True(set.IsProperSubsetOf(ImmutableHashSet.Create([0L, 1L, 2L, 3L])));
    }
}