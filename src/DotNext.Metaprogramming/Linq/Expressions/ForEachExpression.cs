using System;
using System.Collections;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    using static Reflection.CollectionType;
    using static Reflection.DisposableType;

    public sealed class ForEachExpression: Expression, ILoopLabels
    {
        public delegate Expression Statement(MemberExpression current, LabelTarget continueLabel, LabelTarget breakLabel);

        private readonly ParameterExpression enumeratorVar;
        private readonly BinaryExpression enumeratorAssignment;
        private readonly MethodCallExpression moveNextCall;

        private Expression body;

        internal ForEachExpression(Expression collection, LabelTarget continueLabel, LabelTarget breakLabel)
        {
            collection.Type.GetItemType(out var enumerable);
            const string GetEnumeratorMethod = nameof(IEnumerable.GetEnumerator);
            MethodCallExpression getEnumerator;
            const string EnumeratorVarName = "enumerator";
            if (enumerable is null)
            {
                getEnumerator = Call(collection, GetEnumeratorMethod, Array.Empty<Type>());
                if (getEnumerator is null)
                    throw new ArgumentException(ExceptionMessages.EnumerablePatternExpected);
                enumeratorVar = Variable(getEnumerator.Method.ReturnType, EnumeratorVarName);
                moveNextCall = Call(enumeratorVar, nameof(IEnumerator.MoveNext), Array.Empty<Type>());
            }
            else
            {
                getEnumerator = collection.Call(enumerable, GetEnumeratorMethod);
                enumeratorVar = Variable(getEnumerator.Method.ReturnType, EnumeratorVarName);
                //enumerator.MoveNext()
                moveNextCall = enumeratorVar.Call(typeof(IEnumerator), nameof(IEnumerator.MoveNext));
            }
            //enumerator = enumerable.GetEnumerator();
            enumeratorAssignment = Assign(enumeratorVar, getEnumerator);
            Element = Property(enumeratorVar, nameof(IEnumerator.Current));
            BreakLabel = breakLabel ?? Label(typeof(void), "break");
            ContinueLabel = continueLabel ?? Label(typeof(void), "continue");
        }

        public ForEachExpression(Expression collection, Statement body)
            : this(collection, null, null)
        {
            this.body = body(Element, ContinueLabel, BreakLabel);
        }

        public ForEachExpression(Expression collection, Expression body)
            : this(collection, null, null)
        {
            this.body = body;
        }

        public LabelTarget BreakLabel { get; }
        public LabelTarget ContinueLabel { get; }

        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        public MemberExpression Element { get; }

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(void);

        public override bool CanReduce => true;

        public override Expression Reduce()
        {
            Expression loopBody = Condition(moveNextCall, Body, Goto(BreakLabel));
            var disposeMethod = enumeratorVar.Type.GetDisposeMethod();
            loopBody = Loop(loopBody, BreakLabel, ContinueLabel);
            var @finally = disposeMethod is null ?
                    (Expression)Assign(enumeratorVar, Default(enumeratorVar.Type)) :
                    Block(Call(enumeratorVar, disposeMethod), Assign(enumeratorVar, Default(enumeratorVar.Type)));
            loopBody = TryFinally(loopBody, @finally);
            return Block(typeof(void), Sequence.Singleton(enumeratorVar), enumeratorAssignment, loopBody);
        }
    }
}