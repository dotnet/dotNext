using System;
using System.Collections;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.DisposableType;
    using static Reflection.CollectionType;

    internal sealed class ForEachLoopScope: LoopBuilderBase, IExpressionBuilder<BlockExpression>, ICompoundStatement<Action<MemberExpression, LoopCookie>>, ICompoundStatement<Action<MemberExpression>>
    {
        private readonly ParameterExpression enumeratorVar;
        private readonly BinaryExpression enumeratorAssignment;
        private readonly MethodCallExpression moveNextCall;

        internal ForEachLoopScope(Expression collection, LexicalScope parent = null)
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

        private MemberExpression Element => Expression.Property(enumeratorVar, nameof(IEnumerator.Current));

        public new BlockExpression Build()
        {
            Expression loopBody = moveNextCall.Condition(base.Build(), BreakLabel.Goto());
            var disposeMethod = enumeratorVar.Type.GetDisposeMethod();
            loopBody = loopBody.Loop(BreakLabel, ContinueLabel);
            var @finally = disposeMethod is null ?
                    (Expression)enumeratorVar.AssignDefault() :
                    Expression.Block(enumeratorVar.Call(disposeMethod), enumeratorVar.Assign(enumeratorVar.Type.Default()));
            loopBody = loopBody.Finally(@finally);
            return Expression.Block(typeof(void), new[] { enumeratorVar }, enumeratorAssignment, loopBody);
        }

        void ICompoundStatement<Action<MemberExpression, LoopCookie>>.ConstructBody(Action<MemberExpression, LoopCookie> body)
        {
            using (var cookie = new LoopCookie(this))
                body(Element, cookie);
        }

        void ICompoundStatement<Action<MemberExpression>>.ConstructBody(Action<MemberExpression> body) => body(Element);
    }
}
