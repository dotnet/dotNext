using System;
using System.Reflection;
using System.Text;
using Xunit;

namespace DotNext
{
    using Reflection;

    public sealed class MethodPointerTests : Assert
    {
        private static object CreateObject() => new object();

        private interface ICounter
        {
            void Increment();
        }

        private sealed class Counter : ICounter
        {
            internal int Value;

            public void Increment() => Value += 1;
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
        public static void BindUnbind()
        {
            var ptr = new ValueFunc<string, string>(new Func<string, string>(Dup));
            Equal("123123", ptr.Invoke("123"));
            var ptr2 = ptr.Bind("456");
            Equal("456456", ptr2.Invoke());
            ptr = ptr2.Unbind<string>();
            Equal("123123", ptr.Invoke("123"));
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
            var ptr = new ValueFunc<string, int>(new Func<string, int>(int.Parse));
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

        [Fact]
        public static void InstanceMethodCookie()
        {
            var method = typeof(StringBuilder).GetMethod(nameof(ToString), Type.EmptyTypes);
            var cookie = new MethodCookie<StringBuilder, Func<string>, ValueFunc<string>>(method);
            var builder = new StringBuilder("Hello, world!");
            Equal("Hello, world!", cookie.Bind(builder).Invoke());
        }

        [Fact]
        public static void InterfaceMethod()
        {
            var method = typeof(ICounter).GetMethod(nameof(ICounter.Increment));
            var ptr = new ValueAction<ICounter>(method);
            True(ptr.Target is Action<ICounter>);
            var counter = new Counter() { Value = 42 };
            ptr.Invoke(counter);
            Equal(43, counter.Value);
        }

        [Fact]
        public static void Devirtualization()
        {
            var method = typeof(ICounter).GetMethod(nameof(ICounter.Increment));
            var counter = new Counter() { Value = 42 };
            var ptr = new ValueAction<ICounter>(method).Bind(counter);
            True(ptr.Target is Counter);
            ptr.Invoke();
            Equal(43, counter.Value);
        }

        [Fact]
        public static void InterfaceMethodUsingCookie()
        {
            var method = typeof(ICounter).GetMethod(nameof(ICounter.Increment));
            var cookie = new MethodCookie<Action<ICounter>, ValueAction<ICounter>>(method);
            var ptr = cookie.Pointer;
            True(ptr.Target is Action<ICounter>);
            var counter = new Counter() { Value = 42 };
            ptr.Invoke(counter);
            Equal(43, counter.Value);
        }

        [Fact]
        public static void DevirtualizationByMethodCookie()
        {
            var method = typeof(ICounter).GetMethod(nameof(ICounter.Increment));
            var cookie = new MethodCookie<Counter, Action, ValueAction>(method);
            var counter = new Counter() { Value = 42 };
            var ptr = cookie.Bind(counter);
            True(ptr.Target is Counter);
            ptr.Invoke();
            Equal(43, counter.Value);
        }

        [Fact]
        public static void NullPtr()
        {
            Throws<NullReferenceException>(() => new ValueAction().Invoke());
        }
    }
}
