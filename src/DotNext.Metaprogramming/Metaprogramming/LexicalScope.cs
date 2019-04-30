using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal class LexicalScope : LinkedList<Expression>, IDisposable
    {
        internal interface IFactory<out S>
            where S : LexicalScope
        {
            S Create(LexicalScope parent);
        }

        private readonly Dictionary<string, ParameterExpression> variables = new Dictionary<string, ParameterExpression>();

        internal readonly LexicalScope Parent;

        private protected LexicalScope(LexicalScope parent) => Parent = parent;

        internal IReadOnlyDictionary<string, ParameterExpression> Variables => variables;

        public void AddStatement(Expression statement) => AddLast(statement);

        internal void DeclareVariable(ParameterExpression variable) => variables.Add(variable.Name, variable);

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