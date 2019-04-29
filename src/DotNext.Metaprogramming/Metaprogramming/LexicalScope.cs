using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class LexicalScope : LinkedList<Expression>, IDisposable, ILexicalScope
    {
        private readonly Dictionary<string, ParameterExpression> variables = new Dictionary<string, ParameterExpression> ();

        internal readonly LexicalScope Parent;

        internal LexicalScope(LexicalScope parent) => Parent = parent;

        internal IReadOnlyDictionary<string, ParameterExpression> Variables => variables;

        public void AddStatement(Expression statement) => AddLast (statement);

        internal void DeclareVariable(ParameterExpression variable) => variables.Add (variable.Name, variable);

        public Expression Build()
        {
            switch (Count)
            {
                case 0:
                    return Expression.Empty ();
                case 1:
                    if (variables.Count == 0)
                        return First.Value;
                    goto default;
                default:
                    return Expression.Block (variables.Values, this);
            }
        }

        public void Dispose()
        {
            Clear ();
            variables.Clear ();
        }
    }
}