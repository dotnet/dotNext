namespace DotNext
{
    internal interface ISupplier<out TResult>
    {
        TResult Invoke();
    }

    internal interface IConsumer<in T>
    {
        void Invoke(T value);
    }

    internal interface ISupplier<in T1, in T2, out TResult>
    {
        TResult Invoke(T1 arg1, T2 arg2);
    }
}