using System.Runtime.CompilerServices;

namespace DotNext;

using Runtime.CompilerServices;

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

        consumer.As<IFunctional<Action<int>>>().ToDelegate().Invoke(43);
        Equal(43, staticValue1);

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

        consumer.As<IFunctional<Action<int>>>().ToDelegate().Invoke(43);
        Equal(43, staticValue2);

        Equal(consumer, consumer.As<IFunctional<Action<int>>>().ToDelegate());

        static void Consume(int value) => staticValue2 = value;
    }

    [Fact]
    public static void ConsumerWithContext()
    {
        ConsumerClosure<StrongBox<int>, int> consumer = default;
        True(consumer.IsEmpty);
        var box = new StrongBox<int> { Value = 11 };

        unsafe
        {
            consumer = new(&Sum, box);
        }

        False(consumer.IsEmpty);

        consumer.As<IConsumer<int>>().Invoke(30);
        Equal(41, box.Value);

        consumer.As<IFunctional<Action<int>>>().ToDelegate().Invoke(10);
        Equal(51, box.Value);

        static void Sum(in StrongBox<int> box, int arg) => box.Value += arg;
    }

    [Fact]
    public static void ValueAsProducer()
    {
        ValueSupplier<int> supplier = 42;

        Equal(42, supplier.As<ISupplier<int>>().Invoke());
        Equal(42, supplier.As<IFunctional<Func<int>>>().ToDelegate().Invoke());

        Equal("42", supplier.ToString());
    }

    [Fact]
    public static void CtorAsProducer()
    {
        Activator<object> activator = default;

        NotNull(activator.As<ISupplier<object>>().Invoke());
        NotNull(activator.As<IFunctional<Func<object>>>().ToDelegate().Invoke());
    }

    [Fact]
    public static void IdentityProducer()
    {
        SupplierClosure<int, int> supplier = default;
        True(supplier.IsEmpty);

        unsafe
        {
            supplier = new(&Id, 20);
        }

        False(supplier.IsEmpty);

        Equal(20, supplier.As<ISupplier<int>>().Invoke());
        Equal(20, supplier.As<IFunctional<Func<int>>>().ToDelegate().Invoke());

        static int Id(in int value) => value;
    }
}