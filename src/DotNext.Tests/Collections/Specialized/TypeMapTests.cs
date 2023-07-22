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
    public static void InterfaceMethods(ITypeMap<int> map)
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
    public static void GetValueRefOrAddDefaultMethod()
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
    public static void ConcurrentMethods()
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
    public static void DefaultEnumerator()
    {
        var enumerator = default(TypeMap<int>.Enumerator);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static void DefaultConcurrentEnumerator()
    {
        var enumerator = default(ConcurrentTypeMap<int>.Enumerator);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static void EmptyEnumerator()
    {
        var count = 0;
        foreach (ref var item in new TypeMap<int>())
            count++;

        Equal(0, count);
    }

    [Fact]
    public static void EmptyConcurrentEnumerator()
    {
        var count = 0;
        foreach (var item in new ConcurrentTypeMap<int>())
            count++;

        Equal(0, count);
    }

    [Fact]
    public static void NotEmptyEnumerator()
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
    public static void NotEmptyConcurrentEnumerator()
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
}