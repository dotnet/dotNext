using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

internal interface ILexicalScope
{
    void AddStatement(Expression statement);

    ParameterExpression this[string variableName] { get; }

    void DeclareVariable(ParameterExpression variable);
}

/// <summary>
/// Represents lexical scope that can be converted into the expression.
/// </summary>
/// <typeparam name="TExpression">The expression represented by the statement.</typeparam>
/// <typeparam name="TDelegate">The delegate type that points to the method producing a set of instructions inside of lexical scope.</typeparam>
internal interface ILexicalScope<out TExpression, TDelegate> : ILexicalScope
    where TDelegate : MulticastDelegate
{
    /// <summary>
    /// Converts the statement into the expression.
    /// </summary>
    /// <param name="scope">Expression builder.</param>
    /// <returns>The constructed expression.</returns>
    TExpression Build(TDelegate scope);
}