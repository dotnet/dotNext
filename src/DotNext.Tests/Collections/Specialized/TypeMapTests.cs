namespace DotNext.Collections.Specialized;

public sealed class TypeMapTests : Test
{
    public static IEnumerable<object[]> GetMaps()
    {
        yield return new object[] { new TypeMap<int>() };
        yield return new object[] { new TypeMap<int>(1) };
        yield return new object[] { new ConcurrentTypeMap<int>() };
        yield return new object[] { new ConcurrentTypeMap<int>(1) };
    }

    [Theory]
    [MemberData(nameof(GetMaps))]
    public static void MapInterfaceMethods(ITypeMap<int> map)
    {
        False(map.ContainsKey<string>());

        map.Add<string>(42);
        True(map.ContainsKey<string>());

        True(map.TryGetValue<string>(out var result));
        Equal(42, result);

        False(map.ContainsKey<object>());

        map.Clear();
        False(map.ContainsKey<string>());

        map.Set<string>(50);
        True(map.Remove<string>(out result));
        Equal(50, result);

        False(map.Set<string>(42, out _));
        True(map.TryGetValue<string>(out result));
        Equal(42, result);

        True(map.Set<string>(50, out var tmp));
        Equal(42, tmp);
        True(map.TryGetValue<string>(out result));
        Equal(50, result);

        True(map.Remove<string>());
        False(map.ContainsKey<string>());
    }

    [Fact]
    public static void GetValueRefOrAddDefaultMapMethod()
    {
        var map = new TypeMap<long>();
        ref var value = ref map.GetValueRefOrAddDefault<string>(out var exists);
        False(exists);
        Equal(0L, value);
        value = 42L;

        value = ref map.GetValueRefOrAddDefault<string>(out exists);
        True(exists);
        Equal(42L, value);
    }

    [Fact]
    public static void ConcurrentMapMethods()
    {
        var map = new ConcurrentTypeMap<long>();
        True(map.TryAdd<string>(42L));
        True(map.TryGetValue<string>(out var result));
        Equal(42L, result);

        Equal(42L, map.GetOrAdd<string>(60L, out var added));
        False(added);
        True(map.TryGetValue<string>(out result));
        Equal(42L, result);

        False(map.AddOrUpdate<string>(60L));
        True(map.TryGetValue<string>(out result));
        Equal(60L, result);

        True(map.Remove<string>());
        Equal(60L, map.GetOrAdd<string>(60L, out added));
        True(added);

        True(map.Remove<string>());
        True(map.AddOrUpdate<string>(60L));

        True(map.Set<string>(70L, out var tmp));
        Equal(60L, tmp);
    }

    [Theory]
    [MemberData(nameof(GetMaps))]
    public static void ResizeMap(ITypeMap<int> map)
    {
        map.Set<int>(42);
        map.Set<long>(43);
        map.Set<float>(44);
        map.Set<double>(45);

        True(map.TryGetValue<int>(out var result));
        Equal(42, result);

        True(map.TryGetValue<long>(out result));
        Equal(43, result);

        True(map.TryGetValue<float>(out result));
        Equal(44, result);

        True(map.TryGetValue<double>(out result));
        Equal(45, result);
    }

    [Fact]
    public static void DefaultMapEnumerator()
    {
        var enumerator = default(TypeMap<int>.Enumerator);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static void DefaultConcurrentMapEnumerator()
    {
        var enumerator = default(ConcurrentTypeMap<int>.Enumerator);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static void EmptyMapEnumerator()
    {
        var count = 0;
        foreach (ref var item in new TypeMap<int>())
            count++;

        Equal(0, count);
    }

    [Fact]
    public static void EmptyConcurrentMapEnumerator()
    {
        var count = 0;
        foreach (var item in new ConcurrentTypeMap<int>())
            count++;

        Equal(0, count);
    }

    [Fact]
    public static void NotEmptyMapEnumerator()
    {
        var map = new TypeMap<int>();
        map.Set<double>(42);

        var count = 0;
        foreach (ref var item in map)
        {
            Equal(42, item);
            item = 52;
            count++;
        }

        Equal(1, count);
        True(map.TryGetValue<double>(out var value));
        Equal(52, value);
    }

    [Fact]
    public static void NotEmptyConcurrentMapEnumerator()
    {
        var map = new ConcurrentTypeMap<int>();
        map.Set<double>(42);

        var count = 0;
        foreach (var item in map)
        {
            Equal(42, item);
            count++;
        }

        Equal(1, count);
    }

    public static IEnumerable<object[]> GetSets()
    {
        yield return new object[] { new TypeMap() };
        yield return new object[] { new TypeMap(1) };
        yield return new object[] { new ConcurrentTypeMap() };
        yield return new object[] { new ConcurrentTypeMap(1) };
    }

    [Theory]
    [MemberData(nameof(GetSets))]
    public static void SetInterfaceMethods(ITypeMap set)
    {
        False(set.Contains<int>());

        set.Add(42);
        True(set.Contains<int>());

        True(set.TryGetValue<int>(out var result));
        Equal(42, result);

        False(set.Contains<string>());

        set.Clear();
        False(set.Contains<int>());

        set.Set<int>(50);
        True(set.Remove<int>(out result));
        Equal(50, result);

        False(set.Set<int>(42, out _));
        True(set.TryGetValue<int>(out result));
        Equal(42, result);

        True(set.Set<int>(50, out var tmp));
        Equal(42, tmp);
        True(set.TryGetValue<int>(out result));
        Equal(50, result);

        True(set.Remove<int>());
        False(set.Contains<int>());
    }

    [Fact]
    public static void GetValueRefOrAddDefaultSetMethod()
    {
        var map = new TypeMap();
        ref var value = ref map.GetValueRefOrAddDefault<long>(out var exists);
        False(exists);
        Equal(0L, value);
        value = 42L;

        value = ref map.GetValueRefOrAddDefault<long>(out exists);
        True(exists);
        Equal(42L, value);
    }

    [Theory]
    [MemberData(nameof(GetSets))]
    public static void ResizeSet(ITypeMap map)
    {
        map.Set(42);
        map.Set(43L);
        map.Set(44F);
        map.Set(45D);

        True(map.TryGetValue(out int i));
        Equal(42, i);

        True(map.TryGetValue(out long l));
        Equal(43L, l);

        True(map.TryGetValue(out float f));
        Equal(44F, f);

        True(map.TryGetValue(out double d));
        Equal(45, d);
    }
}