using System.Runtime.InteropServices;

namespace DotNext;

using Runtime;
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

    [Fact]
    public static unsafe void Supplier()
    {
        True(default(Supplier<int>).IsEmpty);
        
        delegate*<int> funcPtr = &GetValue;
        Supplier<int> supplier = funcPtr;
        False(supplier.IsEmpty);
        Equal(42, ((Func<int>)supplier).Invoke());
        Equal(42, supplier.As<ISupplier<int>>().Invoke());

        static int GetValue() => 42;
    }

    [Fact]
    public static void DynamicInvoke()
    {
        IFunctional functional = new DelegatingSupplier<int, int, int>(Sum);
        var x = 10;
        var y = 20;
        var result = 0;
        scoped var args = new Variant2 { Var1 = Variant.Immutable(in x), Var2 = Variant.Immutable(in y) };
        functional.DynamicInvoke(in args.Var1, count: 2, Variant.Mutable(ref result));
        Equal(30, result);
        
        static int Sum(int x, int y) => x + y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private ref struct Variant2
    {
        public Variant Var1;
        public Variant Var2;
    }
}