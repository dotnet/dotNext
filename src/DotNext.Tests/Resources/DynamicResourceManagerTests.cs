using System.Diagnostics.CodeAnalysis;
using System.Resources;

namespace DotNext.Resources
{
    [ExcludeFromCodeCoverage]
    public sealed class DynamicResourceManagerTests : Test
    {
        private const string ResourceFileName = "DotNext.TestResources";

        [Fact]
        public static void TestStringResource()
        {
            var manager = new ResourceManager(ResourceFileName, typeof(Test).Assembly);
            Equal("Hello, world!", (string)manager.Get());
        }

        [Fact]
        public static void TestStringResource2()
        {
            var manager = new ResourceManager(ResourceFileName, typeof(Test).Assembly);
            Equal("123", (string)manager.Get());
        }

        [Fact]
        public static void StringTemplate()
        {
            var manager = new ResourceManager(ResourceFileName, typeof(Test).Assembly);
            Equal("Hello, world!", manager.Get().Format("world"));
            Equal("Hello, Henry!", manager.Get().Format("Henry"));
        }

        [Fact]
        public static void ResourceObjects()
        {
            var manager = new ResourceManager(ResourceFileName, typeof(Test).Assembly);
            Equal(42, manager.Get("IntegerValue").As<int>());
            Equal(42L, manager.Get("LongValue").As<long>());
        }

        [Fact]
        public static void NullResourceString()
        {
            var manager = new ResourceManager(ResourceFileName, typeof(Test).Assembly);
            Throws<InvalidOperationException>(() => manager.Get().AsString());
            Throws<InvalidOperationException>(() => manager.Get().AsStream());
            Throws<InvalidOperationException>(() => manager.Get().As<int>());
        }
    }
}