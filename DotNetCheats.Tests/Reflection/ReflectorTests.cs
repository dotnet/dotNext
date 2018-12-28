using System;
using Xunit;

namespace Cheats.Reflection
{
    public sealed class ReflectorTests: Assert
    {
        [Fact]
		public void ConstructorBindingTest()
		{
			var ctor = typeof(string).GetConstructor(new[]{typeof(char), typeof(int)});
			Func<char, int, string> reflected = ctor.Unreflect<Func<char, int, string>>();
            NotNull(reflected);
			Equal("ccc", reflected('c', 3));
            Function<(char, int), string> reflected2 = ctor.Unreflect<Function<(char, int), string>>();
            NotNull(reflected2);
            Equal("ccc", reflected2(('c', 3)));
		}

        [Fact]
        public void InstanceMethodBindingTest()
        {
            var indexOf = typeof(string).GetMethod(nameof(string.IndexOf), new[]{ typeof(char), typeof(int) });
            Func<string, char, int, int> reflected = indexOf.Unreflect<Func<string, char, int, int>>();
            NotNull(reflected);
            Equal(1, reflected("abc", 'b', 0));
            Function<string, (char, int), int> reflected2 = indexOf.Unreflect<Function<string, (char, int), int>>();
            NotNull(reflected2);
            Equal(1, reflected2("abc", ('b', 0)));
        }

        [Fact]
        public void StaticMethodBindingTest()
        {
            var compare = typeof(string).GetMethod(nameof(string.Compare), new[] { typeof(string), typeof(string) });
            var reflected = compare.Unreflect<Func<string, string, int>>();
            NotNull(reflected);
            Equal(1, reflected.Invoke("bb", "aa"));
            var reflected2 = compare.Unreflect<Function<(string, string), int>>();
            NotNull(reflected2);
            Equal(1, reflected2.Invoke(("bb", "aa")));
        }
    }
}