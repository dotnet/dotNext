using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Reflection
{
    [ExcludeFromCodeCoverage]
    public sealed class ExtensionRegistryTest : Test
    {
        private delegate void ZeroMethod(ref int value);
        private delegate bool ContainsMethod(ref string value, char ch);

        private static string ToHex(int value) => value.ToString("X");

        private static void Zero(ref int value) => value = 0;

        private static int GetLength(string value) => value.Length;

        private static bool Contains(ref string value, char ch) => value.IndexOf(ch) >= 0;

        public ExtensionRegistryTest()
        {
            ExtensionRegistry.RegisterInstance(new Func<int, string>(ToHex));
            ExtensionRegistry.RegisterInstance(new ZeroMethod(Zero));
            ExtensionRegistry.RegisterInstance(new Func<string, string>(StringExtensions.Reverse));
            ExtensionRegistry.RegisterStatic<CharEnumerator, Func<string, int>>(GetLength);
            ExtensionRegistry.RegisterStatic<CharEnumerator, ContainsMethod>(new ContainsMethod(Contains));
        }

        [Fact]
        public void StaticExtensionTest()
        {
            Func<string, int> length = Type<CharEnumerator>.Method<string>.RequireStatic<int>(nameof(GetLength));
            var str = "123";
            Equal(3, length(str));
            ContainsMethod contains = Type<CharEnumerator>.Method.Require<ContainsMethod>(nameof(Contains), MethodLookup.Static);
            True(contains(ref str, '3'));
        }

        [Fact]
        public void InstanceExtensionTest()
        {
            Func<int, string> toHex = Type<int>.Method.Require<string>(nameof(ToHex));
            int value = 0xBB;
            Equal("BB", toHex(value));
            ZeroMethod zero = Type<int>.Method.Require<ZeroMethod>(nameof(Zero), MethodLookup.Instance);
            zero(ref value);
            Equal(0, value);
            Func<string, string> reverse = Type<string>.Method.Require<string>(nameof(StringExtensions.Reverse));
            var str = "abc";
            str = reverse(str);
            Equal("cba", str);
        }
    }
}
