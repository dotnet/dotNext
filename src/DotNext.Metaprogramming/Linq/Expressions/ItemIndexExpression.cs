using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Linq.Expressions;

/// <summary>
/// Expresses construction of <see cref="Index"/> value.
/// </summary>
public sealed class ItemIndexExpression : CustomExpression
{
    /// <summary>
    /// The index of the first item in the collection.
    /// </summary>
    public static readonly ItemIndexExpression First;

    /// <summary>
    /// The index of the last item in the collection.
    /// </summary>
    public static readonly ItemIndexExpression Last;

    static ItemIndexExpression()
    {
        var zero = Constant(0);
        First = new ItemIndexExpression(zero);
        Last = new ItemIndexExpression(zero, true);
    }

    private readonly bool conversionRequired;

    /// <summary>
    /// Initializes a new index.
    /// </summary>
    /// <param name="value">The index value.</param>
    /// <param name="fromEnd">A boolean indicating if the index is from the start (<see langword="false"/>) or from the end (<see langword="true"/>) of a collection.</param>
    /// <exception cref="ArgumentException">Type of <paramref name="value"/> should be <see cref="int"/>, <see cref="short"/>, <see cref="byte"/> or <see cref="sbyte"/>.</exception>
    public ItemIndexExpression(Expression value, bool fromEnd = false)
    {
        conversionRequired = Type.GetTypeCode(value.Type) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 => true,
            TypeCode.Int32 => false,
            _ => throw new ArgumentException(ExceptionMessages.TypeExpected<int>(), nameof(value))
        };

        IsFromEnd = fromEnd;
        Value = value;
    }

    /// <summary>
    /// Gets the offset value.
    /// </summary>
    /// <value>The offset value.</value>
    public Expression Value { get; }

    /// <summary>
    /// Gets a value that indicates whether the index is from the start or the end.
    /// </summary>
    /// <value><see langword="true"/> if the Index is from the end; otherwise, <see langword="false"/>.</value>
    public bool IsFromEnd { get; }

    /// <summary>
    /// Gets result type of asynchronous operation.
    /// </summary>
    public override Type Type => typeof(Index);

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Index))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dependency of Index is declared explicitly")]
    internal static Expression GetOffset(Expression index, Expression count)
        => index is ItemIndexExpression { IsFromEnd: false } itemIndex ? itemIndex.Value : Call(index, nameof(Index.GetOffset), null, count);

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    public override Expression Reduce()
    {
        ConstructorInfo? ctor = typeof(Index).GetConstructor([typeof(int), typeof(bool)]);
        Debug.Assert(ctor is not null);
        return New(ctor, conversionRequired ? Convert(Value, typeof(int)) : Value, Constant(IsFromEnd));
    }

    /// <summary>
    /// Visit children expressions.
    /// </summary>
    /// <param name="visitor">Expression visitor.</param>
    /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
    protected override ItemIndexExpression VisitChildren(ExpressionVisitor visitor)
    {
        var expression = visitor.Visit(Value);
        return ReferenceEquals(expression, Value) ? this : new(expression, IsFromEnd);
    }

    internal ItemIndexExpression Visit(ExpressionVisitor visitor)
    {
        var expression = visitor.Visit(this);
        return expression is ItemIndexExpression index ? index : new(expression);
    }
}