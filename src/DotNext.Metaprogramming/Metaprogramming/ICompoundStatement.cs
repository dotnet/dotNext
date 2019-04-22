using System;

namespace DotNext.Metaprogramming
{
    internal interface ICompoundStatement<D>
        where D : Delegate
    {
        void ConstructBody(D body);
    }
}
