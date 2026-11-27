using System.Linq.Expressions;
using DotNext.Reflection;

namespace DotNext.Metaprogramming;

using Collections.Generic;

/// <summary>
/// Represents lambda function builder.
/// </summary>
internal abstract class LambdaExpression : LexicalScope
{
    private protected readonly bool tailCall;

    private protected LambdaExpression(bool tailCall)
        : base(false) => this.tailCall = tailCall;

    private protected static IReadOnlyList<ParameterExpression> GetParameters(System.Reflection.ParameterInfo[] parameters)
        => Array.ConvertAll(parameters, static parameter => Expression.Parameter(parameter.ParameterType, parameter.Name));

    /// <summary>
    /// Gets recursive reference to the lambda.
    /// </summary>
    /// <remarks>
    /// This property can be used to make recursive calls.
    /// </remarks>
    internal abstract Expression Self
    {
        get;
    }

    /// <summary>
    /// Gets lambda parameters.
    /// </summary>
    internal abstract IReadOnlyList<ParameterExpression> Parameters { get; }

    internal abstract Expression Return(Expression? result);
}

/// <summary>
/// Represents lambda function builder.
/// </summary>
/// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
internal sealed class LambdaExpression<TDelegate> : LambdaExpression, ILexicalScope<Expression<TDelegate>, Action<LambdaContext>>, ILexicalScope<Expression<TDelegate>, Action<LambdaContext, ParameterExpression>>, ILexicalScope<Expression<TDelegate>, Func<LambdaContext, Expression>>
    where TDelegate : Delegate
{
    private readonly Type returnType;
    private ParameterExpression? recursion;
    private ParameterExpression? lambdaResult;
    private LabelTarget? returnLabel;

    internal LambdaExpression(bool tailCall)
        : base(tailCall)
    {
        if (!typeof(TDelegate).IsDelegate)
            throw new GenericArgumentException<TDelegate>(ExceptionMessages.AbstractDelegate, nameof(TDelegate));

        var invokeMethod = DelegateType.get_InvokeMethod<TDelegate>();
        Parameters = GetParameters(invokeMethod.GetParameters());
        returnType = invokeMethod.ReturnType;
    }

    /// <summary>
    /// Gets recursive reference to the lambda.
    /// </summary>
    /// <remarks>
    /// This property can be used to make recursive calls.
    /// </remarks>
    internal override ParameterExpression Self => recursion ??= Expression.Variable(typeof(TDelegate), "self");

    /// <summary>
    /// Gets lambda parameters.
    /// </summary>
    internal override IReadOnlyList<ParameterExpression> Parameters { get; }

    private ParameterExpression? Result
    {
        get
        {
            if (returnType.IsVoid)
                return null;
            if (lambdaResult is null)
                DeclareVariable(lambdaResult = Expression.Variable(returnType, "result"));
            return lambdaResult;
        }
    }

    internal override Expression Return(Expression? result)
    {
        var returnStatement = Expression.Return(returnLabel ??= Expression.Label("leave"));

        return Result is { } resultVar
            ? Expression.Block(Expression.Assign(resultVar, result ?? Expression.Default(resultVar.Type)), returnStatement)
            : returnStatement;
    }

    private new Expression<TDelegate> Build()
    {
        if (returnLabel is not null)
            AddStatement(Expression.Label(returnLabel));

        // last instruction should be always a result of a function
        if (lambdaResult is not null)
            AddStatement(lambdaResult);

        // rewrite body
        var body = Expression.Block(returnType, Variables, this);

        // build lambda expression
        if (recursion is not null)
        {
            body = Expression.Block(
                IReadOnlyList<ParameterExpression>.Singleton(recursion),
                Expression.Assign(recursion, Expression.Lambda<TDelegate>(body, tailCall, Parameters)),
                Expression.Invoke(recursion, Parameters));
        }

        return Expression.Lambda<TDelegate>(body, tailCall, Parameters);
    }

    public Expression<TDelegate> Build(Action<LambdaContext> scope)
    {
        using (var context = new LambdaContext(this))
            scope(context);
        return Build();
    }

    public Expression<TDelegate> Build(Action<LambdaContext, ParameterExpression> scope)
    {
        using (var context = new LambdaContext(this))
            scope(context, Result ?? throw new InvalidOperationException(ExceptionMessages.VoidLambda));
        return Build();
    }

    public Expression<TDelegate> Build(Func<LambdaContext, Expression> body)
    {
        using (var context = new LambdaContext(this))
            AddStatement(body(context));
        return Build();
    }

    public override void Dispose()
    {
        lambdaResult = null;
        returnLabel = null;
        recursion = null;
        base.Dispose();
    }
}