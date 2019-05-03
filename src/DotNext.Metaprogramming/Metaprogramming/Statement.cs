namespace DotNext.Metaprogramming
{
    internal abstract class Statement : LexicalScope
    {
        private protected Statement() : base(true) { }

        internal new ILexicalScope Parent => base.Parent;
    }
}