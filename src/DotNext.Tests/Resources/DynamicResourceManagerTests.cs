using Xunit;

namespace DotNext.Resources
{
    public sealed class DynamicResourceManagerTests : Test
    {
        private const string ResourceFileName = "DotNext.TestResources";

        [Fact]
        public static void ResourceStrings()
        {
            dynamic manager = new DynamicResourceManager(ResourceFileName, typeof(Test).Assembly);
            Equal("Hello, world!", manager.TestStringResource);
            Equal("123", manager.TestStringResource2);
            Equal("Hello, world!", manager.StringTemplate("world"));
            Equal("Hello, Henry!", manager.StringTemplate("Henry"));
            Equal("Hello, world!", manager["TestStringResource"]);
            Null(manager.InvalidResource);
        }

        [Fact]
        public static void ResourceObjects()
        {
            dynamic manager = new DynamicResourceManager(ResourceFileName, typeof(Test).Assembly);
            Equal(42, manager.IntegerValue);
            Equal(42L, manager.LongValue);
        }
    }
}