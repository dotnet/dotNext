using System;
using System.Collections;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    using VariantType;
    using static Reflection.CollectionType;
    using static Reflection.DisposableType;

    public sealed class ForEachExpression: Expression, ILoopExpression
    {
        public delegate Expression Statement(MemberExpression current);

        private readonly ParameterExpression enumeratorVar;
        private readonly BinaryExpression enumeratorAssignment;
        private readonly MethodCallExpression moveNextCall;

        private Expression body;

        private ForEachExpression(Expression collection, Variant<Statement, Expression> body)
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
            BreakLabel = Label(typeof(void), "break");
            ContinueLabel = Label(typeof(void), "continue");
            //construct body
            if (body.First.TryGet(out var factory))
                this.body = factory(Element);
            else if (body.Second.TryGet(out var expr))
                this.body = expr;
            else
                this.body = null;
        }

        public ForEachExpression(Expression collection, Statement body)
            : this(collection, new Variant<Statement, Expression>(body))
        {
        }

        public ForEachExpression(Expression collection, Expression body)
            : this(collection, new Variant<Statement, Expression>(body))
        {
        }

        internal ForEachExpression(Expression collection)
            : this(collection, new Variant<Statement, Expression>())
        {
        }

        public LabelTarget BreakLabel { get; }
        public LabelTarget ContinueLabel { get; }

        public Expression Body
        {
            get => body ?? Empty();
            set => body = value;
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