using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

/// <summary>
/// Represents structured exception handling statement.
/// </summary>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/try-catch">try-catch statement</seealso>
public sealed class TryBuilder : ExpressionBuilder<TryExpression>
{
    /// <summary>
    /// Represents constructor of exception handling filter.
    /// </summary>
    /// <param name="exception">The variable representing captured exception.</param>
    /// <returns>The expression of type <see cref="bool"/> indicating that captured exception should be handled.</returns>
    public delegate Expression Filter(ParameterExpression exception);

    /// <summary>
    /// Represents exception handler constructor.
    /// </summary>
    /// <param name="exception">The variable representing captured exception.</param>
    /// <returns>The expression representing exception handling block.</returns>
    public delegate Expression Handler(ParameterExpression exception);

    private readonly Expression tryBlock;
    private readonly ICollection<CatchBlock> handlers;
    private Expression? faultBlock;
    private Expression? finallyBlock;

    internal TryBuilder(Expression tryBlock, ILexicalScope currentScope)
        : base(currentScope)
    {
        this.tryBlock = tryBlock;
        faultBlock = finallyBlock = null;
        handlers = new LinkedList<CatchBlock>();
    }

    internal static bool IsInCatchStatement => LexicalScope.IsInScope<CatchStatement>();

    private TryBuilder Catch(ParameterExpression exception, Expression? filter, Expression handler)
    {
        VerifyCaller();
        handlers.Add(Expression.MakeCatchBlock(exception.Type, exception, handler, filter));
        return this;
    }

    /// <summary>
    /// Constructs exception handling section.
    /// </summary>
    /// <param name="exceptionType">Expected exception.</param>
    /// <param name="filter">Additional filter to be applied to the caught exception.</param>
    /// <param name="handler">Exception handling block.</param>
    /// <returns><c>this</c> builder.</returns>
    public TryBuilder Catch(Type exceptionType, Filter? filter, Handler handler)
    {
        var exception = Expression.Variable(exceptionType, "e");
        return Catch(exception, filter?.Invoke(exception), handler(exception));
    }

    /// <summary>
    /// Constructs exception handling clause.
    /// </summary>
    /// <param name="exceptionType">Expected exception.</param>
    /// <param name="handler">Exception handling block.</param>
    /// <returns><c>this</c> builder.</returns>
    public TryBuilder Catch(Type exceptionType, Handler handler) => Catch(exceptionType, null, handler);

    /// <summary>
    /// Constructs exception handling clause.
    /// </summary>
    /// <typeparam name="TException">Expected exception.</typeparam>
    /// <param name="handler">Exception handling block.</param>
    /// <returns><c>this</c> builder.</returns>
    public TryBuilder Catch<TException>(Handler handler)
        where TException : Exception => Catch(typeof(TException), handler);

    /// <summary>
    /// Constructs exception handling clause that can capture any exception.
    /// </summary>
    /// <param name="handler">The expression representing exception handling clause.</param>
    /// <returns><c>this</c> builder.</returns>
    public TryBuilder Catch(Expression handler) => Catch(Expression.Variable(typeof(Exception), "e"), null, handler);

    /// <summary>
    /// Constructs exception handling section.
    /// </summary>
    /// <param name="exceptionType">Expected exception.</param>
    /// <param name="filter">Additional filter to be applied to the caught exception.</param>
    /// <param name="handler">Exception handling block.</param>
    /// <returns>Structured exception handler builder.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public TryBuilder Catch(Type exceptionType, Filter? filter, Action<ParameterExpression> handler)
    {
        using var statement = new CatchStatement(this, exceptionType, filter);
        return statement.Build(handler);
    }

    /// <summary>
    /// Constructs exception handling section.
    /// </summary>
    /// <param name="exceptionType">Expected exception.</param>
    /// <param name="handler">Exception handling block.</param>
    /// <returns>Structured exception handler.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public TryBuilder Catch(Type exceptionType, Action handler)
    {
        using var statement = new CatchStatement(this, exceptionType);
        return statement.Build(handler);
    }

    /// <summary>
    /// Constructs exception handling section.
    /// </summary>
    /// <param name="exceptionType">Expected exception.</param>
    /// <param name="handler">Exception handling block.</param>
    /// <returns>Structured exception handler.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public TryBuilder Catch(Type exceptionType, Action<ParameterExpression> handler)
        => Catch(exceptionType, null, handler);

    /// <summary>
    /// Constructs exception handling section.
    /// </summary>
    /// <typeparam name="TException">Expected exception.</typeparam>
    /// <param name="handler">Exception handling block.</param>
    /// <returns>Structured exception handler.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public TryBuilder Catch<TException>(Action<ParameterExpression> handler)
        where TException : Exception
        => Catch(typeof(TException), handler);

    /// <summary>
    /// Constructs exception handling section.
    /// </summary>
    /// <typeparam name="TException">Expected exception.</typeparam>
    /// <param name="handler">Exception handling block.</param>
    /// <returns>Structured exception handler.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public TryBuilder Catch<TException>(Action handler)
        where TException : Exception
        => Catch(typeof(TException), handler);

    /// <summary>
    /// Constructs exception handling section that may capture any exception.
    /// </summary>
    /// <param name="handler">Exception handling block.</param>
    /// <returns>Structured exception handler.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public TryBuilder Catch(Action handler)
    {
        using var statement = new CatchStatement(this);
        return statement.Build(handler);
    }

    /// <summary>
    /// Associates expression to be returned from structured exception handling block
    /// in case of any exception.
    /// </summary>
    /// <param name="fault">The expression to be returned from SEH block.</param>
    /// <returns><c>this</c> builder.</returns>
    public TryBuilder Fault(Expression fault)
    {
        VerifyCaller();
        faultBlock = fault;
        return this;
    }

    /// <summary>
    /// Constructs block of code which will be executed in case
    /// of any exception.
    /// </summary>
    /// <param name="fault">Fault handling block.</param>
    /// <returns><c>this</c> builder.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public TryBuilder Fault(Action fault)
    {
        using var statement = new FaultStatement(this);
        return statement.Build(fault);
    }

    /// <summary>
    /// Constructs single expression run when control leaves a <c>try</c> statement.
    /// </summary>
    /// <param name="finally">The single expression to be executed.</param>
    /// <returns><c>this</c> builder.</returns>
    public TryBuilder Finally(Expression @finally)
    {
        VerifyCaller();
        finallyBlock = @finally;
        return this;
    }

    /// <summary>
    /// Constructs block of code run when control leaves a <c>try</c> statement.
    /// </summary>
    /// <param name="body">The block of code to be executed.</param>
    /// <returns><c>this</c> builder.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public TryBuilder Finally(Action body)
    {
        using var statement = new FinallyStatement(this);
        return statement.Build(body);
    }

    private protected override TryExpression Build() => Expression.MakeTry(Type, tryBlock, finallyBlock, faultBlock, handlers);

    private protected override void Cleanup()
    {
        handlers.Clear();
        faultBlock = finallyBlock = null;
        base.Cleanup();
    }

    private sealed class CatchStatement : Statement, ILexicalScope<TryBuilder, Action<ParameterExpression>>, ILexicalScope<TryBuilder, Action>
    {
        private readonly TryBuilder builder;
        private readonly ParameterExpression exception;
        private readonly Expression? filter;

        internal CatchStatement(TryBuilder builder, Type? exceptionType = null, Filter? filter = null)
        {
            this.builder = builder;
            exception = Expression.Variable(exceptionType ?? typeof(Exception), "e");
            this.filter = filter?.Invoke(exception);
        }

        public TryBuilder Build(Action<ParameterExpression> scope)
        {
            scope(exception);
            return builder.Catch(exception, filter, Build());
        }

        public TryBuilder Build(Action scope)
        {
            scope();
            return builder.Catch(exception, filter, Build());
        }
    }

    private sealed class FaultStatement : Statement, ILexicalScope<TryBuilder, Action>
    {
        private readonly TryBuilder builder;

        internal FaultStatement(TryBuilder builder) => this.builder = builder;

        public TryBuilder Build(Action scope)
        {
            scope();
            return builder.Fault(Build());
        }
    }

    private sealed class FinallyStatement : Statement, ILexicalScope<TryBuilder, Action>
    {
        private readonly TryBuilder builder;

        internal FinallyStatement(TryBuilder builder) => this.builder = builder;

        public TryBuilder Build(Action scope)
        {
            scope();
            return builder.Finally(Build());
        }
    }
}