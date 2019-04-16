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
    public sealed class ForEachLoopBuilder: LoopBuilderBase, IExpressionBuilder<BlockExpression>
    {
        private readonly ParameterExpression enumeratorVar;
        private readonly BinaryExpression enumeratorAssignment;
        private readonly MethodCallExpression moveNextCall;

        internal ForEachLoopBuilder(Expression collection, CompoundStatementBuilder parent = null)
            : base(parent)
        {
            collection.Type.GetItemType(out var enumerable);
            const string GetEnumeratorMethod = nameof(IEnumerable.GetEnumerator);
            MethodCallExpression getEnumerator;
            const string EnumeratorVarName = "enumerator";
            if (enumerable is null)
            {
                getEnumerator = collection.Call(GetEnumeratorMethod);
                if (getEnumerator is null)
                    throw new ArgumentException(ExceptionMessages.EnumerablePatternExpected);
                enumeratorVar = Expression.Variable(getEnumerator.Method.ReturnType, EnumeratorVarName);
                moveNextCall = enumeratorVar.Call(nameof(IEnumerator.MoveNext));
            }
            else
            {
                getEnumerator = collection.Call(enumerable, GetEnumeratorMethod);
                enumeratorVar = Expression.Variable(getEnumerator.Method.ReturnType, EnumeratorVarName);
                //enumerator.MoveNext()
                moveNextCall = enumeratorVar.Call(typeof(IEnumerator), nameof(IEnumerator.MoveNext));
            }
            //enumerator = enumerable.GetEnumerator();
            enumeratorAssignment = Expression.Assign(enumeratorVar, getEnumerator);
        }

        /// <summary>
        /// Gets collection element.
        /// </summary>
        public UniversalExpression Element => enumeratorVar.Property(nameof(IEnumerator.Current));

        internal override Expression Build() => Build<BlockExpression, ForEachLoopBuilder>(this);

        BlockExpression IExpressionBuilder<BlockExpression>.Build()
        {
            Expression loopBody = moveNextCall.Condition(base.Build(), breakLabel.Goto());
            var disposeMethod = enumeratorVar.Type.GetDisposeMethod();
            loopBody = loopBody.Loop(breakLabel, continueLabel);
            var @finally = disposeMethod is null ?
                    (Expression)enumeratorVar.AssignDefault() :
                    Expression.Block(enumeratorVar.Call(disposeMethod), enumeratorVar.Assign(enumeratorVar.Type.AsDefault()));
            loopBody = loopBody.Finally(@finally);
            return Expression.Block(typeof(void), new[] { enumeratorVar }, enumeratorAssignment, loopBody);
        }
    }
}
