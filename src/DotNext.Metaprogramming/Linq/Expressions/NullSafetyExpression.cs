using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

using Seq = Collections.Generic.Sequence;

/// <summary>
/// Represents expression that is protected by null check, e.g. safe navigation operator (?. in C#).
/// </summary>
public sealed class NullSafetyExpression : CustomExpression
{
    private readonly BinaryExpression? assignment;
    private readonly bool alwaysNotNull;
    private Expression? body;

    internal NullSafetyExpression(Expression target)
    {
        if (target.Type is { IsPointer: true } or { IsByRefLike: true })
            throw new NotSupportedException(ExceptionMessages.UnsupportedSafeNavigationType(target.Type));

        alwaysNotNull = target.Type.IsValueType && Nullable.GetUnderlyingType(target.Type) is null && Optional.GetUnderlyingType(target.Type) is null;
        if (target is ParameterExpression variable)
        {
            assignment = null;
            Target = variable;
        }
        else
        {
            assignment = Assign(Target = Variable(target.Type, "tmp"), Target);
        }
    }

    /// <summary>
    /// Creates a new safe navigation expression.
    /// </summary>
    /// <param name="target">The expression that is guarded by <see langword="null"/> check.</param>
    /// <param name="body">The body to be executed if <paramref name="target"/> is not <see langword="null"/>. </param>
    /// <returns>The expression representing safe navigation.</returns>
    public static NullSafetyExpression Create(Expression target, Func<ParameterExpression, Expression> body)
    {
        var result = new NullSafetyExpression(target);
        result.Body = body(result.Target);
        return result;
    }

    /// <summary>
    /// Gets expression augmented by <see langword="null"/> check.
    /// </summary>
    public ParameterExpression Target
    {
        get;
    }

    /// <summary>
    /// Gets the body to be executed if <see cref="Target"/> is not <see langword="null"/>.
    /// </summary>
    public Expression Body
    {
        get => body ?? Empty();
        internal set => body = value;
    }

    /// <summary>
    /// Gets type of this expression.
    /// </summary>
    public override Type Type
    {
        get => alwaysNotNull || Body.Type == typeof(void) || Body.Type is { IsClass: true } or { IsInterface: true } || Nullable.GetUnderlyingType(Body.Type) is not null || Optional.GetUnderlyingType(Body.Type) is not null ?
            Body.Type :
            typeof(Nullable<>).MakeGenericType(Body.Type);
    }

    /// <summary>
    /// Reconstructs expression with a new body.
    /// </summary>
    /// <param name="body">The new body of this expression.</param>
    /// <returns>Updated expression.</returns>
    public NullSafetyExpression Update(Expression body)
    {
        var result = assignment is null ? new NullSafetyExpression(Target) : new NullSafetyExpression(assignment.Right);
        result.Body = body;
        return result;
    }

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    public override Expression Reduce()
    {
        // fast path, Target is value type that cannot be null
        if (alwaysNotNull)
            return Body;
        var body = Body.Type.IsValueType ? Convert(Body, Type) : Body;
        Expression conditional = Condition(Target.IsNotNull(), body, Default(body.Type));
        return assignment is null ?
            conditional :
            Block(body.Type, Seq.Singleton(Target), assignment, conditional);
    }
}