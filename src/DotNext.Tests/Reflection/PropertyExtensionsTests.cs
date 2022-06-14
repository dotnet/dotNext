using System.Diagnostics.CodeAnalysis;

namespace DotNext.Reflection
{
    [ExcludeFromCodeCoverage]
    public sealed class PropertyExtensionsTests : Test
    {
        public int Property { get; init; }

        [Fact]
        public static void CheckExternalInit()
        {
            var property = typeof(string).GetProperty(nameof(string.Length));
            False(property.IsExternalInit());

            property = typeof(PropertyExtensionsTests).GetProperty(nameof(Property));
            True(property.IsExternalInit());
        }
    }
}