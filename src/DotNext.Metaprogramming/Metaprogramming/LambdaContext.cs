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

        /// <summary>
        /// Obtains first two arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
            }
            else
                arg1 = arg2 = null;
        }

        /// <summary>
        /// Obtains first three arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        /// <param name="arg3">The expression representing the third argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
                arg3 = scope.Parameters[2];
            }
            else
                arg1 = arg2 = arg3 = null;
        }

        /// <summary>
        /// Obtains first four arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        /// <param name="arg3">The expression representing the third argument.</param>
        /// <param name="arg4">The expression representing the fourth argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
                arg3 = scope.Parameters[2];
                arg4 = scope.Parameters[3];
            }
            else
                arg1 = arg2 = arg3 = arg4 = null;
        }

        /// <summary>
        /// Obtains first five arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        /// <param name="arg3">The expression representing the third argument.</param>
        /// <param name="arg4">The expression representing the fourth argument.</param>
        /// <param name="arg5">The expression representing the fifth argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
                arg3 = scope.Parameters[2];
                arg4 = scope.Parameters[3];
                arg5 = scope.Parameters[4];
            }
            else
                arg1 = arg2 = arg3 = arg4 = arg5 = null;
        }

        /// <summary>
        /// Obtains first six arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        /// <param name="arg3">The expression representing the third argument.</param>
        /// <param name="arg4">The expression representing the fourth argument.</param>
        /// <param name="arg5">The expression representing the fifth argument.</param>
        /// <param name="arg6">The expression representing the sixth argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
                arg3 = scope.Parameters[2];
                arg4 = scope.Parameters[3];
                arg5 = scope.Parameters[4];
                arg6 = scope.Parameters[5];
            }
            else
                arg1 = arg2 = arg3 = arg4 = arg5 = arg6 = null;
        }

        /// <summary>
        /// Obtains first seven arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        /// <param name="arg3">The expression representing the third argument.</param>
        /// <param name="arg4">The expression representing the fourth argument.</param>
        /// <param name="arg5">The expression representing the fifth argument.</param>
        /// <param name="arg6">The expression representing the sixth argument.</param>
        /// <param name="arg7">The expression representing the seventh argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6, out ParameterExpression arg7)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
                arg3 = scope.Parameters[2];
                arg4 = scope.Parameters[3];
                arg5 = scope.Parameters[4];
                arg6 = scope.Parameters[5];
                arg7 = scope.Parameters[6];
            }
            else
                arg1 = arg2 = arg3 = arg4 = arg5 = arg6 = arg7 = null;
        }

        /// <summary>
        /// Obtains first eight arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        /// <param name="arg3">The expression representing the third argument.</param>
        /// <param name="arg4">The expression representing the fourth argument.</param>
        /// <param name="arg5">The expression representing the fifth argument.</param>
        /// <param name="arg6">The expression representing the sixth argument.</param>
        /// <param name="arg7">The expression representing the seventh argument.</param>
        /// <param name="arg8">The expression representing the eighth argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6, out ParameterExpression arg7, out ParameterExpression arg8)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
                arg3 = scope.Parameters[2];
                arg4 = scope.Parameters[3];
                arg5 = scope.Parameters[4];
                arg6 = scope.Parameters[5];
                arg7 = scope.Parameters[6];
                arg8 = scope.Parameters[7];
            }
            else
                arg1 = arg2 = arg3 = arg4 = arg5 = arg6 = arg7 = arg8 = null;
        }

        /// <summary>
        /// Obtains first nine arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        /// <param name="arg3">The expression representing the third argument.</param>
        /// <param name="arg4">The expression representing the fourth argument.</param>
        /// <param name="arg5">The expression representing the fifth argument.</param>
        /// <param name="arg6">The expression representing the sixth argument.</param>
        /// <param name="arg7">The expression representing the seventh argument.</param>
        /// <param name="arg8">The expression representing the eighth argument.</param>
        /// <param name="arg9">The expression representing the ninth argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6, out ParameterExpression arg7, out ParameterExpression arg8, out ParameterExpression arg9)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
                arg3 = scope.Parameters[2];
                arg4 = scope.Parameters[3];
                arg5 = scope.Parameters[4];
                arg6 = scope.Parameters[5];
                arg7 = scope.Parameters[6];
                arg8 = scope.Parameters[7];
                arg9 = scope.Parameters[8];
            }
            else
                arg1 = arg2 = arg3 = arg4 = arg5 = arg6 = arg7 = arg8 = arg9 = null;
        }

        /// <summary>
        /// Obtains first ten arguments in the form of expressions.
        /// </summary>
        /// <param name="arg1">The expression representing the first argument.</param>
        /// <param name="arg2">The expression representing the second argument.</param>
        /// <param name="arg3">The expression representing the third argument.</param>
        /// <param name="arg4">The expression representing the fourth argument.</param>
        /// <param name="arg5">The expression representing the fifth argument.</param>
        /// <param name="arg6">The expression representing the sixth argument.</param>
        /// <param name="arg7">The expression representing the seventh argument.</param>
        /// <param name="arg8">The expression representing the eighth argument.</param>
        /// <param name="arg9">The expression representing the ninth argument.</param>
        /// <param name="arg10">The expression representing the ninth argument.</param>
        public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6, out ParameterExpression arg7, out ParameterExpression arg8, out ParameterExpression arg9, out ParameterExpression arg10)
        {
            if(this.scope.TryGetTarget(out var scope))
            {
                arg1 = scope.Parameters[0];
                arg2 = scope.Parameters[1];
                arg3 = scope.Parameters[2];
                arg4 = scope.Parameters[3];
                arg5 = scope.Parameters[4];
                arg6 = scope.Parameters[5];
                arg7 = scope.Parameters[6];
                arg8 = scope.Parameters[7];
                arg9 = scope.Parameters[8];
                arg10 = scope.Parameters[9];
            }
            else
                arg1 = arg2 = arg3 = arg4 = arg5 = arg6 = arg7 = arg8 = arg9 = arg10 = null;
        }

        /// <summary>
        /// Invokes function recursively.
        /// </summary>
        /// <remarks>
        /// This method doesn't add invocation expression as a statement.
        /// To add recursive call as statement, use <see cref="CodeGenerator.Invoke(Expression, Expression[])"/> instead.
        /// </remarks>
        /// <param name="args">The arguments to be passed into function.</param>
        public InvocationExpression Invoke(params Expression[] args)
            => this.scope.TryGetTarget(out var scope) ? scope.Self.Invoke(args) : null;

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
