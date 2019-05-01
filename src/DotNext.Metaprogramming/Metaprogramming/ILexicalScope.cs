using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal interface ILexicalScope
    {
        void AddStatement(Expression statement);

        ParameterExpression this[string variableName] { get; }

        void DeclareVariable(ParameterExpression variable);
    }

    internal interface ILexicalScope<out E, D> : ILexicalScope
        where E : class
        where D : MulticastDelegate
    {
        E Build(D scope);
    }
}