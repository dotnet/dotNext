using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{

    /// <summary>
    /// Represents basic lexical scope support.
    /// </summary>
    internal class LexicalScope : LinkedList<Expression>, IDisposable, ICompoundStatement<Action>
    {
        private readonly Dictionary<string, ParameterExpression> variables = new Dictionary<string, ParameterExpression>();

        internal readonly LexicalScope Parent;

        internal LexicalScope(LexicalScope parent = null)
        {
            Parent = parent;
        }

        void ICompoundStatement<Action>.ConstructBody(Action body) => body();

        internal IReadOnlyDictionary<string, ParameterExpression> Variables => variables;

        internal void AddStatement(Expression statement) => AddLast(statement);

        internal void DeclareVariable(ParameterExpression variable)
            => variables.Add(variable.Name, variable);

        /// <summary>
        /// Constructs async lambda function capturing the current lexical scope.
        /// </summary>
        /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
        /// <param name="lambda">Lambda function builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        /// <exception cref="ObjectDisposedException">This lexical scope is closed.</exception>
        /// <exception cref="InvalidOperationException">Attempts to call this method for the outer scope from the inner scope.</exception>
        /// <seealso cref="AwaitExpression"/>
        /// <seealso cref="AsyncResultExpression"/>
        /// <seealso cref="AsyncLambdaBuilder{D}"/>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/#BKMK_HowtoWriteanAsyncMethod">Async methods</seealso>
        public LambdaExpression AsyncLambda<D>(Action<AsyncLambdaBuilder<D>> lambda)
            where D : Delegate
        {
            ThrowIfDisposed();
            ThrowIfWrongScope();
            using (var builder = new AsyncLambdaBuilder<D>(this))
                return builder.Build<LambdaExpression, AsyncLambdaBuilder<D>>(lambda);
        }

        /// <summary>
        /// Adds structured exception handling statement.
        /// </summary>
        /// <param name="body"><see langword="try"/> block.</param>
        /// <returns>Structured exception handling builder.</returns>
        /// <exception cref="ObjectDisposedException">This lexical scope is closed.</exception>
        /// <exception cref="InvalidOperationException">Attempts to call this method for the outer scope from the inner scope.</exception>
        public TryBuilder Try(UniversalExpression body) => new TryBuilder(body, this, true);

        /// <summary>
        /// Adds structured exception handling statement.
        /// </summary>
        /// <param name="scope"><see langword="try"/> block builder.</param>
        /// <returns>Structured exception handling builder.</returns>
        /// <exception cref="ObjectDisposedException">This lexical scope is closed.</exception>
        /// <exception cref="InvalidOperationException">Attempts to call this method for the outer scope from the inner scope.</exception>
        public TryBuilder Try(Action<ScopeBuilder> scope)
        {
            Expression tryBlock;
            using (var tryScope = new ScopeBuilder(this))
                tryBlock = tryScope.Build<Expression, ScopeBuilder>(scope);
            return Try(tryBlock);
        }

        /// <summary>
        /// Constructs nested lexical scope.
        /// </summary>
        /// <param name="scope">The code block builder.</param>
        /// <exception cref="ObjectDisposedException">This lexical scope is closed.</exception>
        /// <exception cref="InvalidOperationException">Attempts to call this method for the outer scope from the inner scope.</exception>
        public void Scope(Action<ScopeBuilder> scope)
        {
            Expression statement;
            using (var scopeBuilder = new ScopeBuilder(this))
                statement = scopeBuilder.Build<Expression, ScopeBuilder>(scope);
            AddStatement(statement);
        }

        /// <summary>
        /// Adds selection expression.
        /// </summary>
        /// <param name="switchValue">The value to be handled by the selection expression.</param>
        /// <returns>Selection expression builder.</returns>
        /// <exception cref="ObjectDisposedException">This lexical scope is closed.</exception>
        /// <exception cref="InvalidOperationException">Attempts to call this method for the outer scope from the inner scope.</exception>
        public SwitchBuilder Switch(UniversalExpression switchValue) => new SwitchBuilder(switchValue, this, true);

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
                    return Expression.Block(typeof(void), variables.Values, this);
            }
        }

        public virtual void Dispose()
        {
            Clear();
            variables.Clear();
        }
    }
}
