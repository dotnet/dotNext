using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Reflection
{
    [ExcludeFromCodeCoverage]
    public sealed class EnumTypeTests : Test
    {
        private sealed class TestEnumValueAttribute : Attribute
        {
        }

        private enum EnumWithAttributes
        {
            None = 0,

            [TestEnumValue]
            WithAttribute
        }

        [Fact]
        public static void CustomAttributes()
        {
            Null(EnumWithAttributes.None.GetCustomAttribute<EnumWithAttributes, TestEnumValueAttribute>());
            Empty(EnumWithAttributes.None.GetCustomAttributes<EnumWithAttributes, TestEnumValueAttribute>());

            NotNull(EnumWithAttributes.WithAttribute.GetCustomAttribute<EnumWithAttributes, TestEnumValueAttribute>());
            NotEmpty(EnumWithAttributes.WithAttribute.GetCustomAttributes<EnumWithAttributes, TestEnumValueAttribute>());
        }
    }
}