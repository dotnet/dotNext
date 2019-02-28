using System;
using System.Collections;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.DisposableType;
    using static Reflection.CollectionType;

    /// <summary>
    /// Represents <see langword="foreach"/> loop builder.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
    public sealed class ForEachLoopBuilder: LoopBuilderBase, IExpressionBuilder<Expression>
    {
        private readonly ParameterExpression enumerator;
        private readonly MethodCallExpression moveNextCall;
        private readonly Expression element;

        internal ForEachLoopBuilder(Expression collection, ExpressionBuilder parent)
            : base(parent)
        {
            collection.Type.GetItemType(out var enumerable);
            const string GetEnumeratorMethod = nameof(IEnumerable.GetEnumerator);
            MethodCallExpression getEnumerator;
            if (enumerable is null)
            {
                getEnumerator = collection.Call(GetEnumeratorMethod);
                if (getEnumerator is null)
                    throw new ArgumentException(ExceptionMessages.EnumerablePatternExpected);
                enumerator = Parent.DeclareVariable(getEnumerator.Method.ReturnType, NextName("enumerator_"));
                moveNextCall = enumerator.Call(nameof(IEnumerator.MoveNext));
            }
            else
            {
                getEnumerator = collection.Call(enumerable, GetEnumeratorMethod);
                enumerator = Parent.DeclareVariable(getEnumerator.Method.ReturnType, NextName("enumerator_"));
                //enumerator.MoveNext()
                moveNextCall = enumerator.Call(typeof(IEnumerator), nameof(IEnumerator.MoveNext));
            }
            //enumerator = enumerable.GetEnumerator();
            Parent.Assign(enumerator, getEnumerator);
            //enumerator.Current
            element = enumerator.Property(nameof(IEnumerator.Current));
        }

        /// <summary>
        /// Gets collection element.
        /// </summary>
        public UniversalExpression Element => element;

        internal override Expression Build()
        {
            Expression loopBody = moveNextCall.Condition(base.Build(), breakLabel.Goto());
            var disposeMethod = enumerator.Type.GetDisposeMethod();
            loopBody = loopBody.Loop(breakLabel, continueLabel);
            var @finally = disposeMethod is null ?
                    (Expression)enumerator.AssignDefault() :
                    Expression.Block(enumerator.Call(disposeMethod), enumerator.Assign(enumerator.Type.AsDefault()));
            return loopBody.Finally(@finally);
        }

        Expression IExpressionBuilder<Expression>.Build() => Build();
    }
}
