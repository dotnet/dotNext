using System;
using System.Reflection;
using System.Collections;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Reflection;
    using Threading;

    public sealed class ForEachLoopBuilder: LoopBuilder
    {
        private static long counter = 0L;
        private readonly ParameterExpression enumerator;
        private readonly MethodCallExpression moveNextCall;

        internal ForEachLoopBuilder(Expression collection, ScopeBuilder parent)
            : base(parent)
        {
            collection.Type.GetCollectionElementType(out var enumerable);
            var counter = ForEachLoopBuilder.counter.IncrementAndGet();
            const string GetEnumeratorMethod = nameof(IEnumerable.GetEnumerator);
            MethodCallExpression getEnumerator;
            if (enumerable is null)
            {
                getEnumerator = collection.Call(GetEnumeratorMethod);
                if (getEnumerator is null)
                    throw new ArgumentException("Collection expression doesn't implement IEnumerable interface or GetEnumerator method");
                enumerator = parentScope.DeclareVariable(getEnumerator.Method.ReturnType, "enumerator_" + counter);
                moveNextCall = enumerator.Call(nameof(IEnumerator.MoveNext));
            }
            else
            {
                getEnumerator = collection.Call(enumerable, GetEnumeratorMethod);
                enumerator = parentScope.DeclareVariable(getEnumerator.Method.ReturnType, "enumerator_" + counter);
                //enumerator.MoveNext()
                moveNextCall = enumerator.Call(typeof(IEnumerator), nameof(IEnumerator.MoveNext));
            }
            //enumerator = enumerable.GetEnumerator();
            parentScope.Assign(enumerator, getEnumerator);
            //enumerator.Current
            Element = enumerator.Property(nameof(IEnumerator.Current));
        }

        /// <summary>
        /// Gets collection element.
        /// </summary>
        public Expression Element { get; }

        internal new Expression BuildExpression()
        {
            Expression loopBody = moveNextCall.Condition(this.Upcast<ScopeBuilder, ForEachLoopBuilder>().BuildExpression(), breakLabel.Goto());

            const string DisposeMethodName = nameof(IDisposable.Dispose);
            var disposeMethod = typeof(IDisposable).IsAssignableFrom(enumerator.Type) ?
                typeof(IDisposable).GetMethod(DisposeMethodName) :
                enumerator.Type.GetMethod(DisposeMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, Type.DefaultBinder, Array.Empty<Type>(), Array.Empty<ParameterModifier>());

            
            loopBody = loopBody.Loop(breakLabel, continueLabel);
            return disposeMethod is null ? loopBody : loopBody.Finally(enumerator.Call(disposeMethod));
        }
    }
}
