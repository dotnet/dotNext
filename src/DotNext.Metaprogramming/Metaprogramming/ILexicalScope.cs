using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal interface ILexicalScope
    {
         Expression Build();

        void AddStatement(Expression instruction);
    }
}