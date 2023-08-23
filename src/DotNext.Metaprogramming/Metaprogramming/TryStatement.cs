namespace DotNext.Metaprogramming;

internal sealed class TryStatement : Statement, ILexicalScope<TryBuilder, Action>
{
    public TryBuilder Build(Action scope)
    {
        scope();
        return new TryBuilder(Build(), Parent ?? throw new InvalidOperationException());
    }
}