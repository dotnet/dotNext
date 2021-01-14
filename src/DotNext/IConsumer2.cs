namespace DotNext
{
    public interface IConsumer<in T1, in T2>
    {
        void Invoke(T1 arg1, T2 arg2);
    }
}