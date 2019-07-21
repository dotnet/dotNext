using System;
using System.Reflection;
using Xunit;

namespace DotNext
{
    using Reflection;

    public sealed class MethodPointerTests : Assert
    {
        private static object CreateObject() => new object();

        private sealed class Counter
        {
            internal int Value;

            internal void Increment() => Value += 1;
        }

        private static string Dup(string value) => value + value;

        [Fact]
        public static void CtorTest()
        {
            var activator1 = FunctionPointer<Guid>.CreateActivator();
            Equal(default(Guid), activator1.Invoke());
            var activator2 = FunctionPointer<object>.CreateActivator();
            NotNull(activator2.Invoke());
        }

        [Fact]
        public void StaticMethodAsClosedFunctionPtr()
        {
            var method = GetType().GetMethod(nameof(Dup), BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var obj = "123";
            var ptr = new FunctionPointer<string>(method, obj);
            Equal("123123", ptr.Invoke());
            var cookie = new MethodCookie<string, Func<string>>(method);
            ptr = cookie.CreatePointer("456");
            Equal("456456", ptr.Invoke());
        }
        
        [Fact]
        public static void ParameterlessPointerWithTarget()
        {
            var obj = new Counter();
            var ptr = new ActionPointer(obj.Increment);
            ptr.Invoke();
            Equal(1, obj.Value);
        }

        [Fact]
        public void ParameterlessPointer()
        {
            var ptr = new FunctionPointer<object>(GetType().GetMethod(nameof(CreateObject), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic));
            NotNull(ptr.Invoke());
            ptr = new FunctionPointer<object>(GetType().GetMethod(nameof(CreateObject), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic));
            var d = ptr.ToDelegate();
            NotNull(d.Invoke());
        }

        [Fact]
        public static void UsingMethodPointerSource()
        {
            var factory = new MethodCookie<Func<object>>(CreateObject);
            NotNull(factory.CreatePointer().Invoke());
        }

        [Fact]
        public static void ParseViaPointer()
        {
            var ptr = new FunctionPointer<string, int>(int.Parse);
            Equal(123, ptr.Invoke("123"));
            ptr = default;
            Null(ptr.ToDelegate());
        }

        [Fact]
        public static void FunctionWithTwoParameters()
        {
            var ptr = new FunctionPointer<string, string, string>(string.Concat);
            Equal("Hello, world!", ptr.Invoke("Hello, ", "world!"));
        }

        [Fact]
        public static void FunctionWithThreeParameters()
        {
            var ptr = new FunctionPointer<string, string, string, string>(string.Concat);
            Equal("Hello, world!", ptr.Invoke("Hello", ", ", "world!"));
        }

        [Fact]
        public static void FunctionWithFourParameters()
        {
            var ptr = new FunctionPointer<string, string, string, string, string>(string.Concat);
            Equal("Hello, world!", ptr.Invoke("Hello", ", ", "world", "!"));
        }
    }
}
