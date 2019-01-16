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
            if (enumerable is null)
            {
                var getEnumerator = collection.Type.GetMethod(GetEnumeratorMethod, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy, Type.DefaultBinder, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
                if (getEnumerator is null)
                    throw new ArgumentException("Collection expression doesn't implement IEnumerable interface or GetEnumerator method");
                enumerator = parentScope.DeclareVariable(getEnumerator.ReturnType, "enumerator_" + counter);
                //enumerator = enumerable.GetEnumerator();
                parentScope.Assign(enumerator, Expression.Call(collection, getEnumerator));
                Element = Expression.Property(enumerator, enumerator.Type.GetProperty(nameof(IEnumerator.Current)));
                moveNextCall = Expression.Call(enumerator, enumerator.Type.GetMethod(nameof(IEnumerator.MoveNext), Array.Empty<Type>()));
            }
            else
            {
                var getEnumerator = enumerable.GetMethod(GetEnumeratorMethod, Array.Empty<Type>());
                enumerator = parentScope.DeclareVariable(getEnumerator.ReturnType, "enumerator_" + counter);
                //enumerator = enumerable.GetEnumerator();
                parentScope.Assign(enumerator, Expression.Call(collection, getEnumerator));
                Element = Expression.Property(enumerator, enumerator.Type.GetProperty(nameof(IEnumerator.Current)));
                moveNextCall = Expression.Call(enumerator, typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext)));
            }
        }

        /// <summary>
        /// Gets collection element.
        /// </summary>
        public Expression Element { get; }

        internal new Expression BuildExpression()
        {
            Expression loopBody = Expression.Condition(moveNextCall,
                this.Upcast<ScopeBuilder, ForEachLoopBuilder>().BuildExpression(),
                Expression.Goto(breakLabel),
                typeof(void));

            const string DisposeMethodName = nameof(IDisposable.Dispose);
            var disposeMethod = typeof(IDisposable).IsAssignableFrom(enumerator.Type) ?
                typeof(IDisposable).GetMethod(DisposeMethodName) :
                enumerator.Type.GetMethod(DisposeMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, Type.DefaultBinder, Array.Empty<Type>(), Array.Empty<ParameterModifier>());

            
            loopBody = Expression.Loop(loopBody, breakLabel, continueLabel);
            return disposeMethod is null ? loopBody : Expression.TryFinally(loopBody, Expression.Call(enumerator, disposeMethod));
        }
    }
}
