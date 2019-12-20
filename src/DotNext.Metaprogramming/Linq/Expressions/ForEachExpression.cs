using System;
using System.Collections;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    using static Reflection.CollectionType;
    using static Reflection.DisposableType;

    /// <summary>
    /// Represents iteration over collection elements as expression.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
    public sealed class ForEachExpression : Expression, ILoopLabels
    {
        /// <summary>
        /// Represents constructor of iteration over collection elements.
        /// </summary>
        /// <param name="current">An expression representing current collection item in the iteration.</param>
        /// <param name="continueLabel">A label that can be used to produce <see cref="Expression.Continue(LabelTarget)"/> expression.</param>
        /// <param name="breakLabel">A label that can be used to produce <see cref="Expression.Break(LabelTarget)"/> expression.</param>
        /// <returns></returns>
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
                getEnumerator = collection.Call(collection.Type.GetMethod(GetEnumeratorMethod, Array.Empty<Type>()) ?? throw new ArgumentException(ExceptionMessages.EnumerablePatternExpected));
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

        private ForEachExpression(Expression collection)
            : this(collection, null, null)
        {
        }

        /// <summary>
        /// Creates a new loop expression.
        /// </summary>
        /// <param name="collection">The collection to iterate through.</param>
        /// <param name="body">A delegate that is used to construct the body of the loop.</param>
        /// <returns>The expression instance.</returns>
        public static ForEachExpression Create(Expression collection, Statement body)
        {
            var result = new ForEachExpression(collection);
            result.Body = body(result.Element, result.ContinueLabel, result.BreakLabel);
            return result;
        }

        /// <summary>
        /// Creates a new loop expression.
        /// </summary>
        /// <param name="collection">The collection to iterate through.</param>
        /// <param name="body">The body of the loop.</param>
        /// <returns>The expression instance.</returns>
        public static ForEachExpression Create(Expression collection, Expression body)
            => new ForEachExpression(collection) { Body = body };

        /// <summary>
        /// Gets label that is used by the loop body as a break statement target.
        /// </summary>
        public LabelTarget BreakLabel { get; }

        /// <summary>
        /// Gets label that is used by the loop body as a continue statement target.
        /// </summary>
        public LabelTarget ContinueLabel { get; }

        /// <summary>
        /// Gets loop body.
        /// </summary>
        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        /// <summary>
        /// Gets collection element in the iteration.
        /// </summary>
        public MemberExpression Element { get; }

        /// <summary>
        /// Always returns <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Always returns <see cref="void"/>.
        /// </summary>
        public override Type Type => typeof(void);

        /// <summary>
        /// Always returns <see langword="true"/> because
        /// this expression is <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            Expression loopBody = Condition(moveNextCall, Body, Goto(BreakLabel), typeof(void));
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