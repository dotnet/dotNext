using System;
using System.Reflection;
using Xunit;

namespace DotNext
{
    public sealed class MethodPointerTests : Assert
    {
        private static object CreateObject() => new object();

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
        public static void ParseViaPointer()
        {
            var ptr = new FunctionPointer<string, int>(int.Parse);
            Equal(123, ptr.Invoke("123"));
            ptr = default;
            Null(ptr.ToDelegate());
        }

        [Fact]
        public static void AllocTest()
        {
            var parameters1 = new Func<string, int>(int.Parse).Method.GetParameters();
            var parameters2 = new Func<string, int>(int.Parse).Method.GetParameters();
            Same(parameters1, parameters2);
        }
    }
}
