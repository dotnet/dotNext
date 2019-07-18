namespace DotNext.Threading
{
    internal interface IAtomicWrapper<I, O>
        where I : struct
        where O : struct
    {
        O Convert(I value);
        I Convert(O value);

        Atomic<I> Atomic { get; }

       ref I Reference { get; }
    }
}