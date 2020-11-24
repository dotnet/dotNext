using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Xunit;
using static System.Globalization.CultureInfo;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Reflection
{
    [ExcludeFromCodeCoverage]
    public sealed class ReflectorTests : Test
    {
        [Fact]
        public static void ConstructorBinding()
        {
            var ctor = typeof(string).GetConstructor(new[] { typeof(char), typeof(int) });
            Func<char, int, string> reflected = ctor.Unreflect<Func<char, int, string>>();
            NotNull(reflected);
            Equal("ccc", reflected('c', 3));
            Function<(char, int), string> reflected2 = ctor.Unreflect<Function<(char, int), string>>();
            NotNull(reflected2);
            Equal("ccc", reflected2(('c', 3)));
        }

        [Fact]
        public static void MutableInstanceMethodCall()
        {
            Procedure<object, ValueTuple<int>> setter = typeof(Point).GetProperty("X").SetMethod.Unreflect<Procedure<object, ValueTuple<int>>>();
            NotNull(setter);
            object point = new Point();
            setter.Invoke(point, 42);
            Equal(42, Unbox<Point>(point).X);
        }

        [Fact]
        public static void InstanceMethodBinding()
        {
            var indexOf = typeof(string).GetMethod(nameof(string.IndexOf), new[] { typeof(char), typeof(int) });
            Func<string, char, int, int> reflected = indexOf.Unreflect<Func<string, char, int, int>>();
            NotNull(reflected);
            Equal(1, reflected("abc", 'b', 0));
            Function<string, (char, int), int> reflected2 = indexOf.Unreflect<Function<string, (char, int), int>>();
            NotNull(reflected2);
            Equal(1, reflected2("abc", ('b', 0)));
        }

        [Fact]
        public static void StaticMethodBinding()
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
        public static void TryParseFastInvoke()
        {
            //static
            var method = typeof(long).GetMethod("TryParse", new[] { typeof(string), typeof(long).MakeByRefType() });
            Function<(string text, long result), bool> invoker = method.Unreflect<Function<(string, long), bool>>();
            var args = invoker.ArgList();
            args.text = "100500";
            True(invoker(args));
            Equal(100500L, args.result);
            //untyped
            Function<(object text, object result), object> weakInvoker = method.Unreflect<Function<(object, object), object>>();
            var weakArgs = weakInvoker.ArgList();
            weakArgs.text = "100500";
            True((bool)weakInvoker(weakArgs));
            Equal(100500L, weakArgs.result);
            //partially typed
            Function<(object text, object result), bool> weakInvoker2 = method.Unreflect<Function<(object, object), bool>>();
            weakArgs = weakInvoker.ArgList();
            weakArgs.text = "100500";
            weakArgs.result = null;
            True(weakInvoker2(weakArgs));
            Equal(100500L, weakArgs.result);
        }

        [Fact]
        public static void ToInt32FastInvoke()
        {
            var method = typeof(IntPtr).GetMethod(nameof(IntPtr.ToInt32));
            Function<object, ValueTuple, object> weakInvoker = method.Unreflect<Function<object, ValueTuple, object>>();
            Equal(42, weakInvoker(new IntPtr(42), new ValueTuple()));
        }

        [Fact]
        public static void NewIntPtr()
        {
            var ctor = typeof(IntPtr).GetConstructor(new[] { typeof(int) });
            Function<ValueTuple<int>, IntPtr> factory = ctor.Unreflect<Function<ValueTuple<int>, IntPtr>>();
            var value = factory(new ValueTuple<int>(10));
            Equal(new IntPtr(10), value);

            Function<ValueTuple<object>, IntPtr> ctor2 = ctor.Unreflect<Function<ValueTuple<object>, IntPtr>>();
            value = ctor2(new ValueTuple<object>(42));
            Equal(new IntPtr(42), value);

            Function<ValueTuple<object>, object> ctor3 = ctor.Unreflect<Function<ValueTuple<object>, object>>();
            value = (IntPtr)ctor3(new ValueTuple<object>(50));
            Equal(new IntPtr(50), value);
        }

        private static int staticField;
        private int instanceField;

        [Fact]
        public void UnreflectStaticField()
        {
            ref var field = ref GetType().GetField(nameof(staticField), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly).Unreflect<int>().Value;
            field = 42;
            Equal(staticField, field);
            True(AreSame(ref field, ref staticField));
        }

        [Fact]
        public void UnreflectInstanceField()
        {
            ref var field = ref GetType().GetField(nameof(instanceField), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.NonPublic).Unreflect<ReflectorTests, int>()[this];
            field = 56;
            Equal(instanceField, field);
            True(AreSame(ref field, ref instanceField));
        }

        [Fact]
        public static void UnreflectInterfaceMethod()
        {
            Function<IFormattable, (string, IFormatProvider), string> typedToString = Type<IFormattable>.RequireMethod<(string, IFormatProvider), string>(nameof(IFormattable.ToString));
            NotNull(typedToString);
            IFormattable i = 42;
            Equal("42", typedToString.Invoke(i, null, InvariantCulture));

            Function<object, (object, object), object> untypedToString = typeof(IFormattable).GetMethod(nameof(IFormattable.ToString)).Unreflect<Function<object, (object, object), object>>();
            NotNull(untypedToString);
            Equal("42", untypedToString.Invoke(i, null, InvariantCulture));
        }
    }
}