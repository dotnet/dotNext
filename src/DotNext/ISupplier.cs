namespace DotNext
{
    internal interface ISupplier<out V>
    {
        V Invoke();
    }

    internal interface IConsumer<in T>
    {
        void Invoke(T value);
    }
}