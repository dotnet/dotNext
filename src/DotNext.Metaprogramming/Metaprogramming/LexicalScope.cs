using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents lexical scope in the form of instructions inside it and set of declared local variables.
    /// </summary>
    internal class LexicalScope : ILexicalScope, IDisposable, IEnumerable<Expression>
    {
        private class StatementNode : IDisposable
        {
            internal StatementNode(Expression statement) => Statement = statement;

            internal Expression Statement { get; }

            internal StatementNode? Next { get; private protected set; }

            internal StatementNode CreateNext(Expression statement) => Next = new StatementNode(statement);

            public virtual void Dispose() => Next = null;
        }

        private sealed class Enumerator : StatementNode, IEnumerator<Expression>
        {
            private StatementNode? current;

            internal Enumerator(StatementNode? first)
                : base(Expression.Empty())
            {
                Next = first;
                current = this;
            }

            public bool MoveNext()
            {
                current = Next;
                if (current is null)
                {
                    return false;
                }
                else
                {
                    Next = current.Next;
                    return true;
                }
            }

            public Expression Current => current?.Statement ?? throw new InvalidOperationException();

            object IEnumerator.Current => Current;

            void IEnumerator.Reset() => throw new NotSupportedException();

            public override void Dispose()
            {
                current = null;
                Next = null;
                base.Dispose();
            }
        }

        [ThreadStatic]
        private static LexicalScope? current;

        internal static TScope? FindScope<TScope>()
            where TScope : class, ILexicalScope
        {
            for (var current = LexicalScope.current; current is not null; current = current.Parent)
            {
                if (current is TScope scope)
                    return scope;
            }

            return null;
        }

        internal static bool IsInScope<TScope>()
            where TScope : class, ILexicalScope => FindScope<TScope>() is not null;

        internal static ILexicalScope Current => current ?? throw new InvalidOperationException(ExceptionMessages.OutOfLexicalScope);

        private readonly Dictionary<string, ParameterExpression> variables = new ();

        private StatementNode? first, last;
        private protected readonly LexicalScope? Parent;

        private protected LexicalScope(bool isStatement)
        {
            if (isStatement && current is null)
                throw new InvalidOperationException(ExceptionMessages.OutOfLexicalScope);
            Parent = current;
            current = this;
        }

        ParameterExpression ILexicalScope.this[string variableName]
        {
            get
            {
                for (LexicalScope? current = this; current is not null; current = current.Parent)
                {
                    if (current.variables.TryGetValue(variableName, out var variable))
                        return variable;
                }

                throw new ArgumentException(ExceptionMessages.UndeclaredVariable(variableName), nameof(variableName));
            }
        }

        private protected IReadOnlyCollection<ParameterExpression> Variables => variables.Values;

        public void AddStatement(Expression statement)
            => last = first is null || last is null ? first = new StatementNode(statement) : last.CreateNext(statement);

        public void DeclareVariable(ParameterExpression variable)
        {
            if (string.IsNullOrEmpty(variable.Name))
                throw new ArgumentException(ExceptionMessages.VariableNameIsNullOrEmpty, nameof(variable));
            variables.Add(variable.Name, variable);
        }

        private protected Expression Build()
        {
            if (first is null)
                return Expression.Empty();
            else if (ReferenceEquals(first, last) && Variables.Count == 0)
                return first.Statement;
            else
                return Expression.Block(Variables, this);
        }

        private Enumerator GetEnumerator() => new (first);

        IEnumerator<Expression> IEnumerable<Expression>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public virtual void Dispose()
        {
            for (var current = first; current is not null; current = current.Next)
                current.Dispose();
            first = last = null;
            variables.Clear();
            current = Parent;
        }
    }
}