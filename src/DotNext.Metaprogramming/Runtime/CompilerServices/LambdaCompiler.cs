using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    public readonly struct LambdaCompiler<D>
        where D : Delegate
    {
        private readonly Expression<D> lambda;
        private readonly DebugInfoGenerator debugInfo;

        internal LambdaCompiler(Expression<D> lambda, DebugInfoGenerator debugInfo = null)
        {
            this.lambda = lambda;
            this.debugInfo = debugInfo;
        }

        /// <summary>
        /// Compiles stored lambda expression into delegate.
        /// </summary>
        /// <returns>Compiler lambda expression.</returns>
        public D Compile() => lambda.Compile(debugInfo);

        /// <summary>
        /// Obtains lambda expression that can be compiled using the specified compiler.
        /// </summary>
        /// <param name="compiler">The lambda expression compiler.</param>
        public static implicit operator Expression<D>(LambdaCompiler<D> compiler) => compiler.lambda;
    }
}