namespace DotNext.Threading
{
    internal interface IAtomicWrapper<I, O>
    {
        O Convert(I value);
        I Convert(O value);

        ref I Reference { get; }

        Atomic<I> Atomic { get; }
    }
}