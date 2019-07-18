using System;
using Xunit;

namespace DotNext
{
    public sealed class MethodPointerTests : Assert
    {
        private static object CreateObject() => new object();

        [Fact]
        public static void ParameterlessPointer()
        {
            var ptr = new FunctionPointer<object>(CreateObject);
            NotNull(ptr.Invoke());
            var d = ptr.ToDelegate();
            NotNull(d.Invoke());
        }

        [Fact]
        public static void ParseViaPointer()
        {
            var ptr = new FunctionPointer<string, int>(int.Parse);
            Equal(123, ptr.Invoke("123"));
        }
    }
}
