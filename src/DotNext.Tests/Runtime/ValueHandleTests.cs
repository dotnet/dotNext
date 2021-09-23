namespace DotNext.Runtime
{
    public sealed class ValueHandleTests : Test
    {
        private static long staticField;

        [Fact]
        public static void AllocateStorage()
        {
            var handle = ValueHandle.Allocate<string>(string.Empty);
            True(handle.IsValid);
            Empty(handle.Value);

            handle.Value = "Hello, world!";
            Equal("Hello, world!", handle.Value);
        }

        [Fact]
        public static void ArrayElementAccess()
        {
            int[] array = { 10, 20, 30 };
            var handle = ValueHandle.ArrayElement(array, 1);
            True(handle.IsValid);

            Equal(array[1], handle.Value);

            array[1] = 42;
            Equal(42, handle.Value);

            handle.Value = 43;
            Equal(43, array[1]);
        }

        [Fact]
        public static unsafe void StaticFieldAccess()
        {
            var handle = ValueHandle.Create<long>(&GetStaticFieldRef);
            True(handle.IsValid);

            handle.Value = 42;
            Equal(42, staticField);
            Equal(42, handle.Value);

            static ref long GetStaticFieldRef() => ref staticField;
        }

        [Fact]
        public static void BoxedValueAccess()
        {
            var handle = ValueHandle.Unbox<int>(42);
            Equal(42, handle.Value);
        }

        [Fact]
        public static unsafe void PointerAccess()
        {
            var value = 42;
            var handle = ValueHandle.Pointer<int>(&value);
            True(handle.IsValid);

            Equal(42, handle.Value);

            handle.Value = 43;
            Equal(43, value);
        }

        [Fact]
        public static void SpanAccess()
        {
            var handle = ValueHandle.Allocate<int>(42);
            Equal(42, handle.Span[0]);
        }

        [Fact]
        public static void InvalidHandle()
        {
            var handle = default(ValueHandle<int>);
            False(handle.IsValid);
            Null(handle.ToString());
            Throws<NullReferenceException>(() => handle.Value);
        }
    }
}