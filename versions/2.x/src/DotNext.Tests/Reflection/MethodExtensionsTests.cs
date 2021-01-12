using System.Reflection;
using Xunit;

namespace DotNext.Reflection
{
    public sealed class MethodExtensionsTests : Test
    {
        [Fact]
        public static void TryInvokeMethod()
        {
            var parseMethod = typeof(int).GetMethod(nameof(int.Parse), BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            NotNull(parseMethod);
            Equal(42, parseMethod.TryInvoke(null, "42"));
            NotNull(parseMethod.TryInvoke("Hello, world").Error);
        }
    }
}