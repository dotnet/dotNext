using Expression = System.Linq.Expressions.Expression;
using MulticastDelegate = System.MulticastDelegate;

namespace DotNext.Metaprogramming
{
    internal interface IStatement<out E, D>
        where E : Expression
        where D : MulticastDelegate
    {
        E Build(D scope, ILexicalScope body);
    }
}