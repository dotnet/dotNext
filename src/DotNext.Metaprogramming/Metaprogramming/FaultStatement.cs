namespace DotNext.Metaprogramming;

internal sealed class FaultStatement : Statement, ILexicalScope<TryBuilder, Action>
{
    private readonly TryBuilder builder;

    internal FaultStatement(TryBuilder builder) => this.builder = builder;

    public TryBuilder Build(Action scope)
    {
        scope();
        return builder.Fault(Build());
    }
}
