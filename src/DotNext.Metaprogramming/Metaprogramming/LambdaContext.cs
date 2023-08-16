using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

/// <summary>
/// Represents lambda construction context.
/// </summary>
/// <remarks>
/// The context lifetime is limited by surrounding lexical scope of the lambda function.
/// </remarks>
public readonly struct LambdaContext : IReadOnlyList<ParameterExpression>, IDisposable
{
    private readonly WeakReference? lambda;

    internal LambdaContext(LambdaExpression lambda) => this.lambda = new WeakReference(lambda);

    private LambdaExpression Lambda => lambda?.Target is LambdaExpression result ? result : throw new ObjectDisposedException(nameof(LambdaContext));

    /// <summary>
    /// Gets parameter of the lambda function.
    /// </summary>
    /// <param name="index">The index of the parameter.</param>
    /// <returns>The parameter of lambda function.</returns>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    public ParameterExpression this[int index] => Lambda.Parameters[index];

    /// <summary>
    /// Obtains first two arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
    }

    /// <summary>
    /// Obtains first three arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <param name="arg3">The expression representing the third argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
        arg3 = lambda.Parameters[2];
    }

    /// <summary>
    /// Obtains first four arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <param name="arg3">The expression representing the third argument.</param>
    /// <param name="arg4">The expression representing the fourth argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
        arg3 = lambda.Parameters[2];
        arg4 = lambda.Parameters[3];
    }

    /// <summary>
    /// Obtains first five arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <param name="arg3">The expression representing the third argument.</param>
    /// <param name="arg4">The expression representing the fourth argument.</param>
    /// <param name="arg5">The expression representing the fifth argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
        arg3 = lambda.Parameters[2];
        arg4 = lambda.Parameters[3];
        arg5 = lambda.Parameters[4];
    }

    /// <summary>
    /// Obtains first six arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <param name="arg3">The expression representing the third argument.</param>
    /// <param name="arg4">The expression representing the fourth argument.</param>
    /// <param name="arg5">The expression representing the fifth argument.</param>
    /// <param name="arg6">The expression representing the sixth argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
        arg3 = lambda.Parameters[2];
        arg4 = lambda.Parameters[3];
        arg5 = lambda.Parameters[4];
        arg6 = lambda.Parameters[5];
    }

    /// <summary>
    /// Obtains first seven arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <param name="arg3">The expression representing the third argument.</param>
    /// <param name="arg4">The expression representing the fourth argument.</param>
    /// <param name="arg5">The expression representing the fifth argument.</param>
    /// <param name="arg6">The expression representing the sixth argument.</param>
    /// <param name="arg7">The expression representing the seventh argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6, out ParameterExpression arg7)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
        arg3 = lambda.Parameters[2];
        arg4 = lambda.Parameters[3];
        arg5 = lambda.Parameters[4];
        arg6 = lambda.Parameters[5];
        arg7 = lambda.Parameters[6];
    }

    /// <summary>
    /// Obtains first eight arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <param name="arg3">The expression representing the third argument.</param>
    /// <param name="arg4">The expression representing the fourth argument.</param>
    /// <param name="arg5">The expression representing the fifth argument.</param>
    /// <param name="arg6">The expression representing the sixth argument.</param>
    /// <param name="arg7">The expression representing the seventh argument.</param>
    /// <param name="arg8">The expression representing the eighth argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6, out ParameterExpression arg7, out ParameterExpression arg8)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
        arg3 = lambda.Parameters[2];
        arg4 = lambda.Parameters[3];
        arg5 = lambda.Parameters[4];
        arg6 = lambda.Parameters[5];
        arg7 = lambda.Parameters[6];
        arg8 = lambda.Parameters[7];
    }

    /// <summary>
    /// Obtains first nine arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <param name="arg3">The expression representing the third argument.</param>
    /// <param name="arg4">The expression representing the fourth argument.</param>
    /// <param name="arg5">The expression representing the fifth argument.</param>
    /// <param name="arg6">The expression representing the sixth argument.</param>
    /// <param name="arg7">The expression representing the seventh argument.</param>
    /// <param name="arg8">The expression representing the eighth argument.</param>
    /// <param name="arg9">The expression representing the ninth argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6, out ParameterExpression arg7, out ParameterExpression arg8, out ParameterExpression arg9)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
        arg3 = lambda.Parameters[2];
        arg4 = lambda.Parameters[3];
        arg5 = lambda.Parameters[4];
        arg6 = lambda.Parameters[5];
        arg7 = lambda.Parameters[6];
        arg8 = lambda.Parameters[7];
        arg9 = lambda.Parameters[8];
    }

    /// <summary>
    /// Obtains first ten arguments in the form of expressions.
    /// </summary>
    /// <param name="arg1">The expression representing the first argument.</param>
    /// <param name="arg2">The expression representing the second argument.</param>
    /// <param name="arg3">The expression representing the third argument.</param>
    /// <param name="arg4">The expression representing the fourth argument.</param>
    /// <param name="arg5">The expression representing the fifth argument.</param>
    /// <param name="arg6">The expression representing the sixth argument.</param>
    /// <param name="arg7">The expression representing the seventh argument.</param>
    /// <param name="arg8">The expression representing the eighth argument.</param>
    /// <param name="arg9">The expression representing the ninth argument.</param>
    /// <param name="arg10">The expression representing the tenth argument.</param>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out ParameterExpression arg1, out ParameterExpression arg2, out ParameterExpression arg3, out ParameterExpression arg4, out ParameterExpression arg5, out ParameterExpression arg6, out ParameterExpression arg7, out ParameterExpression arg8, out ParameterExpression arg9, out ParameterExpression arg10)
    {
        var lambda = Lambda;
        arg1 = lambda.Parameters[0];
        arg2 = lambda.Parameters[1];
        arg3 = lambda.Parameters[2];
        arg4 = lambda.Parameters[3];
        arg5 = lambda.Parameters[4];
        arg6 = lambda.Parameters[5];
        arg7 = lambda.Parameters[6];
        arg8 = lambda.Parameters[7];
        arg9 = lambda.Parameters[8];
        arg10 = lambda.Parameters[9];
    }

    /// <summary>
    /// Invokes function recursively.
    /// </summary>
    /// <remarks>
    /// This method doesn't add invocation expression as a statement.
    /// To add recursive call as statement, use <see cref="CodeGenerator.Invoke(Expression, Expression[])"/> instead.
    /// </remarks>
    /// <param name="args">The arguments to be passed into function.</param>
    /// <returns>The invocation expression.</returns>
    /// <exception cref="ObjectDisposedException">This context is no longer available.</exception>
    public InvocationExpression Invoke(params Expression[] args) => Expression.Invoke(Lambda.Self, args);

    /// <inheritdoc/>
    int IReadOnlyCollection<ParameterExpression>.Count => Lambda.Parameters.Count;

    private IEnumerator<ParameterExpression> GetEnumerator() => Lambda.Parameters.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<ParameterExpression> IEnumerable<ParameterExpression>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    void IDisposable.Dispose()
    {
        if (lambda is not null)
            lambda.Target = null;
    }
}