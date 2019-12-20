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

    /// <summary>
    /// Represents lexical scope that can be converted into the expression.
    /// </summary>
    /// <typeparam name="E">The expression represented by the statement.</typeparam>
    /// <typeparam name="D">The delegate type that points to the method producing a set of instructions inside of lexical scope.</typeparam>
    internal interface ILexicalScope<out E, D> : ILexicalScope
        where D : MulticastDelegate
    {
        /// <summary>
        /// Converts the statement into the expression.
        /// </summary>
        /// <param name="scope">The delegate that points to the </param>
        /// <returns></returns>
        E Build(D scope);
    }
}