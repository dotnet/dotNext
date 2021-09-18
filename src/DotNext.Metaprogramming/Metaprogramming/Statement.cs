namespace DotNext.Metaprogramming;

internal class Statement : LexicalScope
{
    private protected Statement()
        : base(true)
    {
    }

    internal new ILexicalScope? Parent => base.Parent;
}