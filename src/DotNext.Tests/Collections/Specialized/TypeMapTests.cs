using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Specialized
{
    [ExcludeFromCodeCoverage]
    public sealed class TypeMapTests : Test
    {
        public static IEnumerable<object[]> GetMaps()
        {
            yield return new object[] { new TypeMap<int>() };
            yield return new object[] { new TypeMap<int>(10) };
            yield return new object[] { new ConcurrentTypeMap<int>() };
            yield return new object[] { new ConcurrentTypeMap<int>(10) };
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

            False(map.Replace<string>(42).HasValue);
            True(map.TryGetValue<string>(out result));
            Equal(42, result);

            Equal(42, map.Replace<string>(50));
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

            Equal(60L, map.Replace<string>(70L));
        }
    }
}