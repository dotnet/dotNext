namespace DotNext.Threading
{
    internal interface IAtomicWrapper<I, O>
    {
        O Convert(I value);
        I Convert(O value);

        Atomic<I> Atomic { get; }
    }
}