namespace DotNext
{
    internal interface ISupplier<out V>
    {
        V Supply();
    }
}