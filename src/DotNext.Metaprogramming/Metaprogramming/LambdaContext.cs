using System;
using System.Collections;
using System.Collections.Generic;
using static System.Linq.Enumerable;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents lambda construction context.
    /// </summary>
    public readonly struct LambdaContext: IReadOnlyList<ParameterExpression>, IDisposable
    {
        private readonly WeakReference<LambdaScope> scope;

        internal LambdaContext(LambdaScope scope) => this.scope = new WeakReference<LambdaScope>(scope);

        /// <summary>
        /// Gets parameter of the lambda function.
        /// </summary>
        /// <param name="index">The index of the parameter.</param>
        /// <returns>The parameter of lambda function.</returns>
        public ParameterExpression this[int index] => this.scope.TryGetTarget(out var scope) ? scope.Parameters[index] : null;

        int IReadOnlyCollection<ParameterExpression>.Count
        {
            get => this.scope.TryGetTarget(out var scope) ? scope.Parameters.Count : 0;
        }

        private IEnumerator<ParameterExpression> GetEnumerator() 
            => (this.scope.TryGetTarget(out var scope) ? scope.Parameters : Empty<ParameterExpression>()).GetEnumerator();

        IEnumerator<ParameterExpression> IEnumerable<ParameterExpression>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal void Dispose() => scope.SetTarget(null);

        void IDisposable.Dispose() => Dispose();

        /// <summary>
        /// Returns expression representing lambda function itself for recursive calls.
        /// </summary>
        /// <param name="context">The lambda construction context.</param>
        public static implicit operator Expression(LambdaContext context) => context.scope.TryGetTarget(out var scope) ? scope.Self : null;
    }
}
