namespace DotNext;

public sealed class FunctionalInterfacesTests : Test
{
    private static int staticValue1, staticValue2;

    [Fact]
    public static void FunctionPointerAsConsumer()
    {
        Consumer<int> consumer = default;
        True(consumer.IsEmpty);

        unsafe
        {
            delegate*<int, void> funcPtr = &Consume;
            consumer = funcPtr;
        }

        False(consumer.IsEmpty);

        consumer.As<IConsumer<int>>().Invoke(42);
        Equal(42, staticValue1);

        static void Consume(int value) => staticValue1 = value;
    }

    [Fact]
    public static void DelegateAsConsumer()
    {
        DelegatingConsumer<int> consumer = default;
        True(consumer.IsEmpty);

        consumer = new Action<int>(Consume);
        False(consumer.IsEmpty);

        consumer.As<IConsumer<int>>().Invoke(42);
        Equal(42, staticValue2);

        static void Consume(int value) => staticValue2 = value;
    }

    [Fact]
    public static void ValueAsProducer()
    {
        ValueSupplier<int> supplier = 42;

        Equal(42, supplier.As<ISupplier<int>>().Invoke());

        Equal("42", supplier.ToString());
    }

    [Fact]
    public static void CtorAsProducer()
    {
        Activator<object> activator = default;

        NotNull(activator.As<ISupplier<object>>().Invoke());
    }
}