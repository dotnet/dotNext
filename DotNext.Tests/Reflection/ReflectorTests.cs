using System;
using Xunit;

namespace DotNext.Reflection
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

        [Fact]
        public void IncorrectFastInvokeTest()
        {
            var invoker = typeof(long).GetMethod("TryParse", new[]{ typeof(string), typeof(long).MakeByRefType() }).AsInvoker<(string, long)>();
            Null(invoker);
        }

        [Fact]
        public void TryParseFastInvokeTest()
        {
            var method = typeof(long).GetMethod("TryParse", new[]{ typeof(string), typeof(long).MakeByRefType() });
            var invoker = method.AsInvoker<(string text, long result, bool success)>();
            var args = invoker.ArgList();
            args.text = "100500";
            invoker(args);
            True(args.success);
            Equal(100500L, args.result);
            var weakInvoker = method.AsInvoker<(object text, object result, object success)>();
            var weakArgs = weakInvoker.ArgList();
            weakArgs.text = "100500";
            weakInvoker(weakArgs);
            True((bool)args.success);
            Equal(100500L, args.result);
        }
        
        [Fact]
        public void ToInt32FastInvokeTest()
        {
            var method = typeof(IntPtr).GetMethod("ToInt32");
            var invoker = method.AsInvoker<(IntPtr value, int result)>();
            var args = invoker.ArgList();
            args.value = new IntPtr(10);
            invoker(args);
            Equal(10, args.result);
            var weakInvoker = method.AsInvoker<(object value, object result)>();
            var weakArgs = weakInvoker.ArgList();
            weakArgs.value = new IntPtr(42);
            weakInvoker(weakArgs);
            Equal(42, weakArgs.result);
        }

        [Fact]
        public void Test()
        {
            System.Reflection.MethodInfo IndexOfReflected = typeof(string).GetMethod(nameof(string.IndexOf), new[] { typeof(char), typeof(int) }, Array.Empty<System.Reflection.ParameterModifier>());

            MemberInvoker<(string instance, char ch, int index, int result)> FastInvoker = IndexOfReflected.AsInvoker<(string instance, char ch, int index, int result)>();

            MemberInvoker<(object instance, object ch, object index, object result)> UntypedFastInvoker = IndexOfReflected.AsInvoker<(object instance, object ch, object index, object result)>();
        }
    }
}