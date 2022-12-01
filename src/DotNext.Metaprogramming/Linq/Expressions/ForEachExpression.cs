using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions;

using static Reflection.CollectionType;
using static Reflection.DisposableType;
using Seq = Collections.Generic.Sequence;

/// <summary>
/// Represents iteration over collection elements as expression.
/// </summary>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in">foreach Statement</seealso>
public sealed class ForEachExpression : CustomExpression, ILoopLabels
{
    private const string EnumeratorVarName = "enumerator";
    private const string BreakLabelName = "break";
    private const string ContinueLabelName = "continue";

    /// <summary>
    /// Represents constructor of iteration over collection elements.
    /// </summary>
    /// <param name="current">An expression representing current collection item in the iteration.</param>
    /// <param name="continueLabel">A label that can be used to produce <see cref="Expression.Continue(LabelTarget)"/> expression.</param>
    /// <param name="breakLabel">A label that can be used to produce <see cref="Expression.Break(LabelTarget)"/> expression.</param>
    /// <returns>The constructed loop body.</returns>
    public delegate Expression Statement(MemberExpression current, LabelTarget continueLabel, LabelTarget breakLabel);

    private readonly ParameterExpression enumeratorVar;
    private readonly BinaryExpression enumeratorAssignment;
    private readonly MethodCallExpression moveNextCall;
    private readonly bool? configureAwait;  // null for synchronous expression

    private Expression? body;

    // for synchronous collection
    internal ForEachExpression(Expression collection, LabelTarget? continueLabel, LabelTarget? breakLabel)
    {
        collection.Type.GetItemType(out var enumerable);
        const string GetEnumeratorMethod = nameof(IEnumerable.GetEnumerator);
        MethodCallExpression getEnumerator;

        if (enumerable is null)
        {
            getEnumerator = collection.Call(collection.Type.GetMethod(GetEnumeratorMethod, Type.EmptyTypes) ?? throw new ArgumentException(ExceptionMessages.EnumerablePatternExpected));
            enumeratorVar = Variable(getEnumerator.Method.ReturnType, EnumeratorVarName);
            moveNextCall = Call(enumeratorVar, nameof(IEnumerator.MoveNext), Type.EmptyTypes);
        }
        else
        {
            getEnumerator = collection.Call(enumerable, GetEnumeratorMethod);
            enumeratorVar = Variable(getEnumerator.Method.ReturnType, EnumeratorVarName);

            // enumerator.MoveNext()
            moveNextCall = enumeratorVar.Call(typeof(IEnumerator), nameof(IEnumerator.MoveNext));
        }

        // enumerator = enumerable.GetEnumerator();
        enumeratorAssignment = Assign(enumeratorVar, getEnumerator);
        Element = Property(enumeratorVar, nameof(IEnumerator.Current));
        BreakLabel = breakLabel ?? Label(typeof(void), BreakLabelName);
        ContinueLabel = continueLabel ?? Label(typeof(void), ContinueLabelName);
    }

    // for asynchronous collection
    internal ForEachExpression(Expression collection, Expression cancellationToken, bool configureAwait, LabelTarget? continueLabel, LabelTarget? breakLabel)
    {
        collection.Type.GetItemType(out var enumerable);
        if (enumerable is null or { IsConstructedGenericType: false } || enumerable.GetGenericTypeDefinition() != typeof(IAsyncEnumerable<>))
            throw new ArgumentException(ExceptionMessages.AsyncEnumerableExpected, nameof(collection));
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new ArgumentException(ExceptionMessages.TypeExpected<CancellationToken>(), nameof(cancellationToken));

        this.configureAwait = configureAwait;

        // enumerator = enumerable.GetAsyncEnumerator(token);
        const string GetAsyncEnumeratorMethod = nameof(IAsyncEnumerable<Missing>.GetAsyncEnumerator);
        MethodCallExpression getEnumerator = collection.Call(GetAsyncEnumeratorMethod, cancellationToken);
        enumeratorVar = Variable(getEnumerator.Type, EnumeratorVarName);
        enumeratorAssignment = Assign(enumeratorVar, getEnumerator);

        // discover async MoveNext
        moveNextCall = enumeratorVar.Call(nameof(IAsyncEnumerator<Missing>.MoveNextAsync));

        Element = Property(enumeratorVar, nameof(IAsyncEnumerator<Missing>.Current));
        BreakLabel = breakLabel ?? Label(typeof(void), BreakLabelName);
        ContinueLabel = continueLabel ?? Label(typeof(void), ContinueLabelName);
    }

    private ForEachExpression(Expression collection)
        : this(collection, null, null)
    {
    }

    private ForEachExpression(Expression collection, Expression cancellationToken, bool configureAwait)
        : this(collection, cancellationToken, configureAwait, null, null)
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
    /// Creates asynchronous loop expression.
    /// </summary>
    /// <param name="collection">The collection to iterate through.</param>
    /// <param name="cancellationToken">The expression of type <see cref="CancellationToken"/>.</param>
    /// <param name="configureAwait"><see langword="true"/> to call <see cref="ValueTask.ConfigureAwait(bool)"/> with <see langword="false"/> argument when awaiting <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> method.</param>
    /// <param name="body">A delegate that is used to construct the body of the loop.</param>
    /// <returns>The expression instance.</returns>
    /// <seealso cref="IAsyncEnumerable{T}"/>
    /// <seealso cref="IsAwaitable"/>
    public static ForEachExpression Create(Expression collection, Expression cancellationToken, bool configureAwait, Statement body)
    {
        var result = new ForEachExpression(collection, cancellationToken, configureAwait);
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
        => new(collection) { Body = body };

    /// <summary>
    /// Creates asynchronous loop expression.
    /// </summary>
    /// <param name="collection">The collection to iterate through.</param>
    /// <param name="cancellationToken">The expression of type <see cref="CancellationToken"/>.</param>
    /// <param name="configureAwait"><see langword="true"/> to call <see cref="ValueTask.ConfigureAwait(bool)"/> with <see langword="false"/> argument when awaiting <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> method.</param>
    /// <param name="body">The body of the loop.</param>
    /// <returns>The expression instance.</returns>
    /// <seealso cref="IAsyncEnumerable{T}"/>
    /// <seealso cref="IsAwaitable"/>
    public static ForEachExpression Create(Expression collection, Expression cancellationToken, bool configureAwait, Expression body)
        => new(collection, cancellationToken, configureAwait) { Body = body };

    /// <summary>
    /// Indicates that this expression represents enumeration over asynchronous collection.
    /// </summary>
    public bool IsAwaitable => configureAwait.HasValue;

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
    /// Always returns <see cref="void"/>.
    /// </summary>
    public override Type Type => typeof(void);

    private Expression Reduce(Expression moveNextCall, Expression? disposeCall)
    {
        Expression loopBody = Condition(moveNextCall, Body, Goto(BreakLabel), Type);
        loopBody = Loop(loopBody, BreakLabel, ContinueLabel);
        Expression @finally = disposeCall is null ?
                Assign(enumeratorVar, Default(enumeratorVar.Type)) :
                Block(disposeCall, Assign(enumeratorVar, Default(enumeratorVar.Type)));
        loopBody = TryFinally(loopBody, @finally);
        return Block(Type, Seq.Singleton(enumeratorVar), enumeratorAssignment, loopBody);
    }

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    public override Expression Reduce()
    {
        Expression moveNextCall = this.moveNextCall;
        Expression? disposeCall;
        MethodInfo? disposeMethod;

        if (this.configureAwait.TryGetValue(out var configureAwait))
        {
            moveNextCall = moveNextCall.Await(configureAwait);
            disposeMethod = enumeratorVar.Type.GetDisposeAsyncMethod();
            Debug.Assert(disposeMethod is not null);
            disposeCall = Call(enumeratorVar, disposeMethod).Await(configureAwait);
        }
        else
        {
            disposeMethod = enumeratorVar.Type.GetDisposeMethod();
            disposeCall = disposeMethod is null ? null : Call(enumeratorVar, disposeMethod);
        }

        return Reduce(moveNextCall, disposeCall);
    }
}