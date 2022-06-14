using System.Diagnostics.CodeAnalysis;

namespace DotNext.Reflection
{
    [ExcludeFromCodeCoverage]
    public sealed class RecordConceptTests : Test
    {
        public record class RecordClass(int A);

        [Fact]
        public static void CreateCopy()
        {
            var obj = new RecordClass(42);
            var copy = Record<RecordClass>.Clone(obj);
            NotSame(obj, copy);
        }

        [Fact]
        public static void Bind()
        {
            var obj = new RecordClass(42);
            NotSame(obj, Record<RecordClass>.Bind(obj).Invoke());
        }
    }
}