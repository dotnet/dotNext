using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace DotNext.Linq.Expressions;

/// <summary>
/// Represents unsigned right shift expression.
/// </summary>
public sealed class UnsignedRightShiftExpression : CustomExpression
{
    private const string SpecialName = "op_UnsignedRightShift";
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static;

    /// <summary>
    /// Initializes a new unsigned right shift expression.
    /// </summary>
    /// <param name="expr">The left operand.</param>
    /// <param name="shiftAmount">The shift amount.</param>
    /// <exception cref="ArgumentException"><paramref name="expr"/> doesn't support unsigned right shift operator.</exception>
    public UnsignedRightShiftExpression(Expression expr, Expression shiftAmount)
    {
        Left = expr;
        Right = shiftAmount;

        var shiftOperatorInterface = typeof(IShiftOperators<,,>).MakeGenericType(expr.Type, shiftAmount.Type, expr.Type);
        if (shiftOperatorInterface.IsAssignableFrom(expr.Type))
        {
            var map = expr.Type.GetInterfaceMap(shiftOperatorInterface);
            var index = Array.FindIndex(map.InterfaceMethods, static candidate => candidate.Name is SpecialName);
            if (index >= 0)
            {
                Method = map.TargetMethods[index];
                return;
            }
        }

        Method = expr.GetType().GetMethod(SpecialName, Flags, null, [], null) is { IsSpecialName: true } method
            ? method
            : throw new ArgumentException(ExceptionMessages.InterfaceNotImplemented(expr.Type, typeof(IShiftOperators<,,>)), nameof(expr));
    }

    /// <summary>
    /// Represents a method that implements unsigned right shift.
    /// </summary>
    public MethodInfo Method { get; }
    
    /// <summary>
    /// Represents left operand.
    /// </summary>
    public Expression Left { get; }
    
    /// <summary>
    /// Represents right operand.
    /// </summary>
    public Expression Right { get; }

    /// <inheritdoc/>
    protected override UnsignedRightShiftExpression VisitChildren(ExpressionVisitor visitor)
    {
        var left = visitor.Visit(Left);
        var right = visitor.Visit(Right);

        return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
            ? this
            : new(left, right);
    }

    /// <inheritdoc/>
    public override Expression Reduce()
    {
        if (Right.Type == typeof(int))
        {
            switch (Type.GetTypeCode(Left.Type))
            {
                case TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64:
                    return RightShift(Left, Right);
                case TypeCode.SByte:
                    return ConvertAndShift<byte>(Left, Right);
                case TypeCode.Int16:
                    return ConvertAndShift<ushort>(Left, Right);
                case TypeCode.Int32:
                    return ConvertAndShift<uint>(Left, Right);
                case TypeCode.Int64:
                    return ConvertAndShift<ulong>(Left, Right);
            }
        }
        
        return Call(Method, Left, Right);
        
        static UnaryExpression ConvertAndShift<T>(Expression left, Expression right)
            where T : struct, IBinaryInteger<T>
            => Convert(RightShift(Convert(left, typeof(T)), right), left.Type);
    }
}