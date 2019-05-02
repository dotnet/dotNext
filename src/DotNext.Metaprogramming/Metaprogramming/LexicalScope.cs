using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal class LexicalScope : LinkedList<Expression>, ILexicalScope, IDisposable
    {
        [ThreadStatic]
        private static LexicalScope current;

        internal static S FindScope<S>()
            where S : class, ILexicalScope
        {
            for(var current = LexicalScope.current; !(current is null); current = current.Parent)
                if(current is S scope)
                    return scope;
            return null;
        }

        internal static bool IsInScope<S>() where S : class, ILexicalScope => !(FindScope<S>() is null);

        internal static ILexicalScope Current => current ?? throw new InvalidOperationException(ExceptionMessages.OutOfLexicalScope);

        private readonly Dictionary<string, ParameterExpression> variables = new Dictionary<string, ParameterExpression>();

        private protected readonly LexicalScope Parent;

        private protected LexicalScope(bool isStatement)
        {
            if(isStatement && current is null)
                throw new InvalidOperationException(ExceptionMessages.OutOfLexicalScope);
            Parent = current;
            current = this;
        }

        ParameterExpression ILexicalScope.this[string variableName]
        {
            get
            {
                for (var current = this; !(current is null); current = current.Parent)
                    if (current.variables.TryGetValue(variableName, out var variable))
                        return variable;
                return null;
            }
        }

        void ILexicalScope.AddStatement(Expression statement) => AddLast(statement);

        private protected void DeclareVariable(ParameterExpression variable) => variables.Add(variable.Name, variable);

        void ILexicalScope.DeclareVariable(ParameterExpression variable) => DeclareVariable(variable);

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
            current = Parent;
        }
    }
}