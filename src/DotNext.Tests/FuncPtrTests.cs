using System;
using Xunit;

namespace DotNext
{
    public sealed class FuncPtrTests : Assert
    {
        private static object CreateObject() => new object();

        [Fact]
        public static void ParameterlessPointer()
        {
            var ptr = new StaticFunc<object>(CreateObject);
            NotNull(ptr.Invoke());
            var d = ptr.ToDelegate();
            NotNull(d.Invoke());
        }

        [Fact]
        public static void ToStringViaPointer()
        {
            var obj = new object();
            var ptr = new StaticFunc<object, string>(new Func<string>(obj.ToString).Method);
            obj = ptr.Invoke("Hello");
        }
    }
}
