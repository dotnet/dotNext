using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
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

        private static object DynamicInvoke<T>(T callable, params object[] args)
            where T : struct, ICallable
            => callable.DynamicInvoke(args);

        [Fact]
        public static void CtorTest()
        {
            var activator1 = ValueFunc<Guid>.Activator;
            False(activator1.IsEmpty);
            Equal(default, activator1.Invoke());
            Equal(default(Guid), DynamicInvoke(activator1));
            var activator2 = ValueFunc<object>.Activator;
            False(activator1.IsEmpty);
            NotNull(activator2.Invoke());
            NotNull(DynamicInvoke(activator2));
            False(activator1.Equals(activator2));
        }

        [Fact]
        public static void DefaultTest()
        {
            var activator1 = ValueFunc<Guid>.DefaultValueProvider;
            Equal(default, activator1.Invoke());
            Equal(default(Guid), DynamicInvoke(activator1));
            var activator2 = ValueFunc<object>.DefaultValueProvider;
            Null(activator2.Invoke());
            Null(DynamicInvoke(activator2));
            False(activator1.Equals(activator2));
        }

        [Fact]
        public void StaticMethodAsClosedFunctionPtr()
        {
            var method = GetType().GetMethod(nameof(Dup), BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var obj = "123";
            var ptr = new ValueFunc<string>(method.CreateDelegate<Func<string>>(obj));
            False(ptr.IsEmpty);
            Equal("123123", ptr.Invoke());
            Equal("123123", DynamicInvoke(ptr));
        }

        [Fact]
        public static void ParameterlessPointerWithTarget()
        {
            var obj = new Counter();
            var ptr = new ValueAction(obj.Increment);
            ptr.Invoke();
            Equal(1, obj.Value);
            Null(DynamicInvoke(ptr));
            Equal(2, obj.Value);
            False(ptr.Equals(null));
            False(ptr.Equals(default));
            False(ptr.IsEmpty);
            True(ptr.Equals(ptr));
        }

        [Fact]
        public static void PointerWithTarget()
        {
            var ptr = new ValueFunc<StringComparison, int>("Hello, world".GetHashCode);
            False(ptr.IsEmpty);
            NotEqual(0, ptr.Invoke(StringComparison.OrdinalIgnoreCase));
            NotEqual(0, DynamicInvoke(ptr, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ParameterlessPointer()
        {
            var ptr = new ValueFunc<object>(GetType().GetMethod(nameof(CreateObject), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic));
            False(ptr.IsEmpty);
            NotNull(ptr.Invoke());
            NotNull(DynamicInvoke(ptr));
            ptr = new ValueFunc<object>(GetType().GetMethod(nameof(CreateObject), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic));
            var d = ptr.ToDelegate();
            NotNull(d.Invoke());
        }

        [Fact]
        public static void ParseViaPointer()
        {
            var ptr = new ValueFunc<string, int>(new Func<string, int>(int.Parse));
            False(ptr.IsEmpty);
            Equal(123, ptr.Invoke("123"));
            Equal(123, ptr.ToDelegate().Invoke("123"));
            Equal(123, DynamicInvoke(ptr, "123"));
            ptr = default;
            Null(ptr.ToDelegate());
            Throws<NullReferenceException>(() => ptr.Invoke("123"));
        }

        [Fact]
        public static void FunctionWithTwoParameters()
        {
            var ptr = new ValueFunc<string, string, string>(string.Concat);
            False(ptr.IsEmpty);
            Equal("Hello, world!", ptr.Invoke("Hello, ", "world!"));
            Equal("12", ptr.Invoke("1", "2"));
            Equal("34", ptr.ToDelegate().Invoke("3", "4"));
        }

        [Fact]
        public static void FunctionWithThreeParameters()
        {
            var ptr = new ValueFunc<string, string, string, string>(string.Concat);
            False(ptr.IsEmpty);
            Equal("Hello, world!", ptr.Invoke("Hello", ", ", "world!"));
            Equal("123", ptr.ToDelegate().Invoke("1", "2", "3"));
            Equal("123", DynamicInvoke(ptr, "1", "2", "3"));
        }

        [Fact]
        public static void FunctionWithFourParameters()
        {
            var ptr = new ValueFunc<string, string, string, string, string>(string.Concat);
            False(ptr.IsEmpty);
            Equal("Hello, world!", ptr.Invoke("Hello", ", ", "world", "!"));
            Equal("1234", DynamicInvoke(ptr, "1", "2", "3", "4"));
            Equal("1234", ptr.ToDelegate().Invoke("1", "2", "3", "4"));
        }

        [Fact]
        public static void OpenInstanceMethod()
        {
            var method = typeof(StringBuilder).GetMethod(nameof(ToString), Type.EmptyTypes);
            var ptr = new ValueFunc<StringBuilder, string>(method);
            Null(ptr.Target);
            False(ptr.IsEmpty);
            var builder = new StringBuilder("Hello, world!");
            Equal("Hello, world!", ptr.Invoke(builder));
            Equal("Hello, world!", DynamicInvoke(ptr, builder));
        }

        [Fact]
        public static void InterfaceMethod()
        {
            var method = typeof(ICounter).GetMethod(nameof(ICounter.Increment));
            var ptr = new ValueAction<ICounter>(method);
            var counter = new Counter() { Value = 42 };
            ptr.Invoke(counter);
            Equal(43, counter.Value);
            Null(DynamicInvoke(ptr, counter));
            Equal(44, counter.Value);
        }


        [Fact]
        public static void NullPtr()
        {
            Throws<NullReferenceException>(new ValueAction().Invoke);
        }

        [Fact]
        public static void OperatorAsDelegate()
        {
            var op = DelegateHelpers.CreateOpenDelegate<Func<decimal, decimal>>(arg => -arg);
            var ptr = new ValueFunc<decimal, decimal>(op);
            Equal(-10M, ptr.Invoke(10M));
        }

        private static string ToString(int value) => value.ToString();

        [Fact]
        public static void ConverterAsFunc()
        {
            var ptr = new Converter<int, string>(ToString).AsValueFunc(true);
            Equal("42", ptr.Invoke(42));
            Equal("42", DynamicInvoke(ptr, 42));
        }

        private static bool IsNegative(int value) => value < 0;

        [Fact]
        public static void PredicateAsFunc()
        {
            var predicate = new Predicate<int>(IsNegative).AsValueFunc();
            True(predicate.Invoke(-1));
            False(predicate.Invoke(0));

            var converter = (Converter<int, bool>)predicate;
            True(converter.Invoke(-1));
            False(converter.Invoke(0));
        }

        [Fact]
        public static void AugmentedValueFuncConstruction()
        {
            var ptr = ValueFuncFactory.CreateSumFunction();
            NotSame(ptr.ToDelegate(), ptr.ToDelegate());
            Equal(42UL, ptr.Invoke(40UL, 2UL));
        }

        private static void ComputeSum(ref long x, long y)
            => x += y;

        [Fact]
        public static void RefActionCall()
        {
            var action = new ValueRefAction<long, long>(ComputeSum);
            False(action.IsEmpty);
            var i = 10L;
            action.Invoke(ref i, 32L);
            Equal(42L, i);
            var args = new object[] { 1L, 10L };
            Null(DynamicInvoke(action, args));
            Equal(11L, args[0]);
            var array = new[] { 1L, 2L, 3L };
            array.ForEach(action);
            Equal(1L, array[0]);
            Equal(3L, array[1]);
            Equal(5L, array[2]);
        }

        private struct StructForTest
        {
            internal long Value;
#pragma warning disable CS0649
            internal int Field1, Field2, Field3;
#pragma warning restore CS0649

            public void Add(long value) => Value += value;
        }

        [Fact]
        public static void RefActionInstanceCall()
        {
            var method = typeof(StructForTest).GetMethod(nameof(StructForTest.Add), new[] { typeof(long) });
            var action = new ValueRefAction<StructForTest, long>(method);
            var i = new StructForTest { Value = 12L };
            action.Invoke(ref i, 30L);
            Equal(42L, i.Value);
        }

        [Fact]
        public static void ComparisonDelegate()
        {
            Comparison<int> cmp = (x, y) => x.CompareTo(y);
            var func = cmp.AsValueFunc(true);
            False(func.IsEmpty);
            True(func.Invoke(1, 2) < 0);
            NotNull(DynamicInvoke(func, 1, 2));
            True(func.Invoke(2, 1) > 0);
            False(func.Invoke(1, 2) >= 0);
            False(func.Invoke(2, 1) <= 0);
        }
    }
}
