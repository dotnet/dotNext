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
            var activator1 = ValueFunc<Guid>.Activator;
            Equal(default, activator1.Invoke());
            var activator2 = ValueFunc<object>.Activator;
            NotNull(activator2.Invoke());
        }

        [Fact]
        public static void DefaultTest()
        {
            var activator1 = ValueFunc<Guid>.DefaultValueProvider;
            Equal(default, activator1.Invoke());
            var activator2 = ValueFunc<object>.DefaultValueProvider;
            Null(activator2.Invoke());
        }

        [Fact]
        public static void PredicateTest()
        {
            True(ValuePredicate<int>.True.Invoke(10));
            False(ValuePredicate<int>.False.Invoke(10));
        }

        [Fact]
        public void StaticMethodAsClosedFunctionPtr()
        {
            var method = GetType().GetMethod(nameof(Dup), BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var obj = "123";
            var ptr = new ValueFunc<string>(method, obj);
            Equal("123123", ptr.Invoke());
            var cookie = new MethodCookie<string, Func<string>, ValueFunc<string>>(method);
            ptr = cookie & "456";
            Equal("456456", ptr.Invoke());
        }
        
        [Fact]
        public static void ParameterlessPointerWithTarget()
        {
            var obj = new Counter();
            var ptr = new ValueAction(obj.Increment);
            ptr.Invoke();
            Equal(1, obj.Value);
        }

        [Fact]
        public void ParameterlessPointer()
        {
            var ptr = new ValueFunc<object>(GetType().GetMethod(nameof(CreateObject), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic));
            NotNull(ptr.Invoke());
            ptr = new ValueFunc<object>(GetType().GetMethod(nameof(CreateObject), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic));
            var d = ptr.ToDelegate();
            NotNull(d.Invoke());
        }

        [Fact]
        public static void UsingMethodPointerSource()
        {
            var factory = new MethodCookie<Func<object>, ValueFunc<object>>(CreateObject);
            NotNull(factory.Pointer.Invoke());
        }

        [Fact]
        public static void ParseViaPointer()
        {
            var ptr = new ValueFunc<string, int>(int.Parse);
            Equal(123, ptr.Invoke("123"));
            ptr = default;
            Null(ptr.ToDelegate());
        }

        [Fact]
        public static void FunctionWithTwoParameters()
        {
            var ptr = new ValueFunc<string, string, string>(string.Concat);
            Equal("Hello, world!", ptr.Invoke("Hello, ", "world!"));
        }

        [Fact]
        public static void FunctionWithThreeParameters()
        {
            var ptr = new ValueFunc<string, string, string, string>(string.Concat);
            Equal("Hello, world!", ptr.Invoke("Hello", ", ", "world!"));
            ptr = new MethodCookie<Func<string, string, string, string>, ValueFunc<string, string, string, string>>(string.Concat);
            Equal("Hello, world!", ptr.Invoke("Hello", ", ", "world!"));
        }

        [Fact]
        public static void FunctionWithFourParameters()
        {
            var ptr = new ValueFunc<string, string, string, string, string>(string.Concat);
            Equal("Hello, world!", ptr.Invoke("Hello", ", ", "world", "!"));
        }
    }
}
