using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal interface ILexicalScope<out E, D>
        where E : class
        where D : MulticastDelegate
    {
        E Build(D scope);
    }
}