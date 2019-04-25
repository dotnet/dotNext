using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents basic lexical scope support.
    /// </summary>
    internal abstract class LexicalScope : LinkedList<Expression>, IDisposable, ICompoundStatement<Action>
    {
        private readonly Dictionary<string, ParameterExpression> variables = new Dictionary<string, ParameterExpression>();

        internal readonly LexicalScope Parent;

        private protected LexicalScope(LexicalScope parent) => Parent = parent;

        void ICompoundStatement<Action>.ConstructBody(Action body) => body();

        internal IReadOnlyDictionary<string, ParameterExpression> Variables => variables;

        internal void AddStatement(Expression statement) => AddLast(statement);

        internal void DeclareVariable(ParameterExpression variable)
            => variables.Add(variable.Name, variable);

        private protected Expression Build()
        {
            switch (Count)
            {
                case 0:
                    return Expression.Empty();
                case 1:
                    if (variables.Count == 0)
                        return First.Value;
                    goto default;
                default:
                    return Expression.Block(variables.Values, this);
            }
        }

        public virtual void Dispose()
        {
            Clear();
            variables.Clear();
        }
    }
}
