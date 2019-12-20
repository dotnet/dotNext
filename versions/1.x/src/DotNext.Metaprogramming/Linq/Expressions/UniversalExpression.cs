using System;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Represents any expression with full support
    /// of overloaded operators and conversion from
    /// primitive data types.
    /// </summary>
    /// <remarks>
    /// This class is intended for expression building purposes only.
    /// </remarks>
    public readonly struct UniversalExpression : IExpressionBuilder<Expression>, IDynamicMetaObjectProvider, IEquatable<UniversalExpression>
    {
        private readonly Expression expression;

        /// <summary>
        /// Wraps regular Expression Tree node into universal expression.
        /// </summary>
        /// <param name="expr">An expression to be wrapped.</param>
        private UniversalExpression(Expression expr) => expression = expr;

        /// <summary>
        /// Gets the static type of this expression.
        /// </summary>
        public Type Type => expression?.Type ?? typeof(void);

        /// <summary>
        /// Gets the node type of this expression.
        /// </summary>
        public ExpressionType NodeType => expression is null ? ExpressionType.Default : expression.NodeType;

        /// <summary>
        /// Wraps regular Expression Tree node into universal expression.
        /// </summary>
        /// <param name="expr">An expression to be wrapped.</param>
        /// <returns>Universal expression representing original expression.</returns>
        public static explicit operator UniversalExpression(Expression expr) => new UniversalExpression(expr);

        /// <summary>
        /// Converts universal expression into regular LINQ expression.
        /// </summary>
        /// <param name="view">Universal expression to be converted.</param>
        public static implicit operator Expression(UniversalExpression view) => view.expression ?? Expression.Empty();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UniversalExpression Transform(Converter<Expression, Expression> converter) => new UniversalExpression(converter(expression ?? Expression.Empty()));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UniversalExpression Transform<T>(Func<Expression, T, Expression> converter, T arg)
            => new UniversalExpression(converter(expression ?? Expression.Empty(), arg));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UniversalExpression Transform<T1, T2>(Func<Expression, T1, T2, Expression> converter, T1 arg1, T2 arg2)
            => new UniversalExpression(converter(expression ?? Expression.Empty(), arg1, arg2));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UniversalExpression Transform<T1, T2, T3>(Func<Expression, T1, T2, T3, Expression> converter, T1 arg1, T2 arg2, T3 arg3)
            => new UniversalExpression(converter(expression ?? Expression.Empty(), arg1, arg2, arg3));

        /// <summary>
        /// Constructs constant value of <see cref="long"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(long value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="ulong"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(ulong value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="int"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(int value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="uint"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(uint value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="short"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(short value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="ushort"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(ushort value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="byte"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(byte value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="sbyte"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(sbyte value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="bool"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(bool value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="string"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(string value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="decimal"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(decimal value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="float"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(float value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="double"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(double value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs constant value of <see cref="char"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(char value) => new UniversalExpression(value.Const());

        /// <summary>
        /// Constructs formatted string expression.
        /// </summary>
        /// <param name="value">Formatter string representation.</param>
        public static implicit operator UniversalExpression(FormattableString value) => new UniversalExpression(InterpolationExpression.PlainString(value));

        /// <summary>
        /// Logical NOT expression.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Constructed logical NOT expression.</returns>
        public static UniversalExpression operator !(UniversalExpression expr) => expr.Transform(ExpressionBuilder.Not);

        /// <summary>
        /// Ones complement.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Ones complement expression.</returns>
        public static UniversalExpression operator ~(UniversalExpression expr) => expr.Transform(ExpressionBuilder.OnesComplement);

        /// <summary>
        /// Unary plus.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UniversalExpression operator +(UniversalExpression expr) => expr.Transform(ExpressionBuilder.UnaryPlus);

        /// <summary>
        /// Unary minus.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UniversalExpression operator -(UniversalExpression expr) => expr.Transform(ExpressionBuilder.Negate);

        /// <summary>
        /// Binary arithmetic addition expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator +(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Add, right.expression);

        /// <summary>
        /// Binary arithmetic addition expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator +(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Add, right);

        /// <summary>
        /// Binary arithmetic subtraction expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator -(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Subtract, right.expression ?? Expression.Empty());

        /// <summary>
        /// Binary arithmetic subtraction expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator -(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Subtract, right);

        /// <summary>
        /// "greater than" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.GreaterThan, right.expression ?? Expression.Empty());

        /// <summary>
        /// "greater than" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.GreaterThan, right);

        /// <summary>
        /// "greater than or equal" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >=(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.GreaterThanOrEqual, right.expression ?? Expression.Empty());

        /// <summary>
        /// "greater than or equal" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >=(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.GreaterThanOrEqual, right);

        /// <summary>
        /// "less than" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.LessThan, right.expression ?? Expression.Empty());

        /// <summary>
        /// "less than" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.LessThan, right);

        /// <summary>
        /// "less than or equal" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <=(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.LessThanOrEqual, right.expression ?? Expression.Empty());

        /// <summary>
        /// "less than or equal" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <=(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.LessThanOrEqual, right);

        /// <summary>
        /// Binary arithmetic multiplication expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator *(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Multiply, right.expression ?? Expression.Empty());

        /// <summary>
        /// Binary arithmetic multiplication expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator *(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Multiply, right);

        /// <summary>
        /// Binary arithmetic division expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator /(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Divide, right.expression ?? Expression.Empty());

        /// <summary>
        /// Binary arithmetic division expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator /(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Divide, right);

        /// <summary>
        /// Binary logical OR expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator |(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Or, right.expression ?? Expression.Empty());

        /// <summary>
        /// Binary logical OR expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator |(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Or, right);

        /// <summary>
        /// Binary logical AND expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator &(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.And, right.expression ?? Expression.Empty());

        /// <summary>
        /// Binary logical AND expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator &(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.And, right);

        /// <summary>
        /// Binary logical exclusive OR expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator ^(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Xor, right.expression ?? Expression.Empty());

        /// <summary>
        /// Binary logical exclusive OR expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator ^(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Xor, right);

        /// <summary>
        /// Equality comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator ==(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Equal, right.expression ?? Expression.Empty());

        /// <summary>
        /// Equality comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator ==(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Equal, right);

        /// <summary>
        /// Inequality comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator !=(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.NotEqual, right.expression ?? Expression.Empty());

        /// <summary>
        /// Inequality comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator !=(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.NotEqual, right);

        /// <summary>
        /// Arithmetic remainder expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator %(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Modulo, right.expression ?? Expression.Empty());

        /// <summary>
        /// Arithmetic remainder expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator %(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Modulo, right);

        /// <summary>
        /// Bitwise right-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >>(UniversalExpression left, int right)
            => left.Transform(ExpressionBuilder.RightShift, Expression.Constant(right));

        /// <summary>
        /// Bitwise left-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <<(UniversalExpression left, int right)
            => left.Transform(ExpressionBuilder.LeftShift, Expression.Constant(right));

        /// <summary>
        /// Constructs raising a number to a power expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        [SuppressMessage("Style", "IDE1006")]
        [SuppressMessage("Style", "CA1707", Justification = "This is special name of the operation method")]
        public static UniversalExpression op_Exponent(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.Power, right.expression ?? Expression.Empty());

        /// <summary>
        /// Constructs raising a number to a power expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        [SuppressMessage("Style", "IDE1006")]
        [SuppressMessage("Style", "CA1707", Justification = "This is special name of the operation method")]
        public static UniversalExpression op_Exponent(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.Power, right);

        /// <summary>
        /// Bitwise left-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        [SuppressMessage("Style", "IDE1006")]
        [SuppressMessage("Style", "CA1707", Justification = "This is special name of the operation method")]
        public static UniversalExpression op_LeftShift(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.LeftShift, right.expression ?? Expression.Empty());

        /// <summary>
        /// Bitwise left-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        [SuppressMessage("Style", "IDE1006")]
        [SuppressMessage("Style", "CA1707", Justification = "This is special name of the operation method")]
        public static UniversalExpression op_LeftShift(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.LeftShift, right);

        /// <summary>
        /// Bitwise right-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        [SuppressMessage("Style", "IDE1006")]
        [SuppressMessage("Style", "CA1707", Justification = "This is special name of the operation method")]
        public static UniversalExpression op_RightShift(UniversalExpression left, UniversalExpression right)
            => left.Transform(ExpressionBuilder.RightShift, right.expression ?? Expression.Empty());

        /// <summary>
        /// Bitwise right-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        [SuppressMessage("Style", "IDE1006")]
        [SuppressMessage("Style", "CA1707", Justification = "This is special name of the operation method")]
        public static UniversalExpression op_RightShift(UniversalExpression left, Expression right)
            => left.Transform(ExpressionBuilder.RightShift, right);

        /// <summary>
        /// Constructs suspension point in the execution of the lambda function until the awaited task completes.
        /// </summary>
        /// <returns><see langword="await"/> expression.</returns>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await">Await expression</seealso>
        public UniversalExpression Await() => new UniversalExpression((expression ?? Expression.Empty()).Await());

        /// <summary>
        /// Constructs type conversion expression.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>The type conversion expression.</returns>
        public UniversalExpression Convert(Type targetType) => new UniversalExpression(expression.Convert(targetType));

        /// <summary>
        /// Constructs type conversion expression.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The type conversion expression.</returns>
        public UniversalExpression Convert<T>() => Transform(ExpressionBuilder.Convert<T>);

        /// <summary>
        /// Constructs type check expression.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>The type test expression.</returns>
        public UniversalExpression InstanceOf(Type targetType) => new UniversalExpression(expression?.InstanceOf(targetType) ?? (Expression)false.Const());

        /// <summary>
        /// Constructs type check expression.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The type test expression.</returns>
        public UniversalExpression InstanceOf<T>() => Transform(ExpressionBuilder.InstanceOf<T>);

        /// <summary>
        /// Constructs an expression that represents an explicit
        /// reference or boxing conversion where <see langword="null"/> is supplied if the conversion fails.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>Type conversion expression.</returns>
        public UniversalExpression TryConvert(Type targetType) => new UniversalExpression((expression ?? Expression.Empty()).TryConvert(targetType));

        /// <summary>
        /// Constructs an expression that represents an explicit
        /// reference or boxing conversion where <see langword="null"/> is supplied if the conversion fails.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>Type conversion expression.</returns>
        public UniversalExpression TryConvert<T>() => Transform(ExpressionBuilder.TryConvert<T>);

        /// <summary>
        /// Binary expression that represents a conditional
        /// OR operation that evaluates the second operand only if the this expression evaluates to <see langword="false"/>.
        /// </summary>
        /// <param name="other">The second operand.</param>
        /// <returns>Binary expression.</returns>
        public UniversalExpression OrElse(Expression other) => Transform(ExpressionBuilder.OrElse, other);

        /// <summary>
        /// Binary expression that represents a conditional
        /// OR operation that evaluates the second operand only if the this expression evaluates to <see langword="false"/>.
        /// </summary>
        /// <param name="other">The second operand.</param>
        /// <returns>Binary expression.</returns>
        public UniversalExpression OrElse(UniversalExpression other) => OrElse(other.expression ?? Expression.Empty());

        /// <summary>
        /// Constructs binary expression that represents a conditional
        /// AND operation that evaluates the second operand only if the this expression evaluates to <see langword="true"/>.
        /// </summary>
        /// <param name="other">The second operand.</param>
        /// <returns>Binary expression.</returns>
        public UniversalExpression AndAlso(Expression other) => Transform(ExpressionBuilder.AndAlso, other);

        /// <summary>
        /// Constructs binary expression that represents a conditional
        /// AND operation that evaluates the second operand only if the this expression evaluates to <see langword="true"/>.
        /// </summary>
        /// <param name="other">The second operand.</param>
        /// <returns>Binary expression.</returns>
        public UniversalExpression AndAlso(UniversalExpression other) => AndAlso(other.expression ?? Expression.Empty());

        /// <summary>
        /// Explicit unboxing.
        /// </summary>
        /// <param name="targetType">The target value type.</param>
        /// <returns>Unboxing expression.</returns>
        public UniversalExpression Unbox(Type targetType) => Transform(ExpressionBuilder.Unbox, targetType);

        /// <summary>
        /// Explicit unboxing.
        /// </summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <returns>Unboxing expression.</returns>
        public UniversalExpression Unbox<T>() where T : struct => Transform(ExpressionBuilder.Unbox<T>);

        /// <summary>
        /// Constructs delegate invocation expression.
        /// </summary>
        /// <param name="arguments">Invocation arguments.</param>
        /// <returns>Invocation expression.</returns>
        public UniversalExpression Invoke(params Expression[] arguments) => Transform(ExpressionBuilder.Invoke, arguments);

        /// <summary>
        /// Constructs array element access expression.
        /// </summary>
        /// <param name="indexes">Array element indicies.</param>
        /// <returns>Array element access expression.</returns>
        public UniversalExpression ElementAt(params Expression[] indexes) => Transform(ExpressionBuilder.ElementAt, indexes);

        /// <summary>
        /// Returns expression representing array length.
        /// </summary>
        /// <returns>Array length expression.</returns>
        public UniversalExpression ArrayLength() => Transform(ExpressionBuilder.ArrayLength);

        /// <summary>
        /// Constructs instance method call expression.
        /// </summary>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public UniversalExpression Call(MethodInfo method, params Expression[] arguments) => Transform(ExpressionBuilder.Call, method, arguments);

        /// <summary>
        /// Constructs instance method call expression.
        /// </summary>
        /// <param name="methodName">The name of the method to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public UniversalExpression Call(string methodName, params Expression[] arguments) => Transform(ExpressionBuilder.Call, methodName, arguments);

        /// <summary>
        /// Constructs interface or base class method call expression.
        /// </summary>
        /// <param name="interfaceType">The interface or base class.</param>
        /// <param name="methodName">The name of the method in the interface or base class to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public UniversalExpression Call(Type interfaceType, string methodName, params Expression[] arguments)
            => Transform(ExpressionBuilder.Call, interfaceType, methodName, arguments);

        /// <summary>
        /// Constructs instance property or indexer access expression.
        /// </summary>
        /// <param name="property">Property metadata.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public UniversalExpression Property(PropertyInfo property, params Expression[] indicies)
            => Transform(ExpressionBuilder.Property, property, indicies);

        /// <summary>
        /// Constructs instance property or indexer access expression declared in the given interface or base type. 
        /// </summary>
        /// <param name="interfaceType">The interface or base class declaring property.</param>
        /// <param name="propertyName">The name of the instance property or indexer.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public UniversalExpression Property(Type interfaceType, string propertyName, params Expression[] indicies)
            => Transform(ExpressionBuilder.Property, interfaceType, propertyName, indicies);

        /// <summary>
        /// Constructs instance property or indexer access expression.
        /// </summary>
        /// <param name="propertyName">The name of the instance property or indexer.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public UniversalExpression Property(string propertyName, params Expression[] indicies)
            => Transform(ExpressionBuilder.Property, propertyName, indicies);

        /// <summary>
        /// Constructs instance field access expression.
        /// </summary>
        /// <param name="field">Field metadata.</param>
        /// <returns>Field access expression.</returns>
        public UniversalExpression Field(FieldInfo field) => Transform(ExpressionBuilder.Field, field);

        /// <summary>
        /// Constructs instance field access expression.
        /// </summary>
        /// <param name="fieldName">The name of the instance field.</param>
        /// <returns>Field access expression.</returns>
        public UniversalExpression Field(string fieldName) => Transform(ExpressionBuilder.Field, fieldName);

        /// <summary>
        /// Constructs string concatenation expression.
        /// </summary>
        /// <param name="other">Other strings to concatenate.</param>
        /// <returns>An expression presenting concatenation.</returns>
        public UniversalExpression Concat(params Expression[] other) => Transform(ExpressionBuilder.Concat, other);

        /// <summary>
        /// Constructs loop statement which has a body equal to this expression.
        /// </summary>
        /// <param name="break">Optional loop break label which will installed automatically.</param>
        /// <param name="continue">Optional loop continuation which will be installed automatically.</param>
        /// <returns>Loop statement.</returns>
        public UniversalExpression Loop(LabelTarget @break, LabelTarget @continue) => Transform(ExpressionBuilder.Loop, @break, @continue);

        /// <summary>
        /// Constructs loop statement which has a body equal to this expression.
        /// </summary>
        /// <param name="break">Optional loop break label which will installed automatically.</param>
        /// <returns>Loop statement.</returns>
        public UniversalExpression Loop(LabelTarget @break) => Transform(ExpressionBuilder.Loop, @break);

        /// <summary>
        /// Constructs loop statement which has a body equal to this expression.
        /// </summary>
        /// <returns>Loop statement.</returns>
        public UniversalExpression Loop() => Transform(Expression.Loop);

        /// <summary>
        /// Constructs conditional expression.
        /// </summary>
        /// <param name="ifTrue">Positive branch.</param>
        /// <param name="ifFalse">Negative branch.</param>
        /// <param name="type">The type of conditional expression. Default is <see cref="void"/>.</param>
        /// <returns>Conditional expression.</returns>
        public UniversalExpression Condition(Expression ifTrue = null, Expression ifFalse = null, Type type = null)
            => new UniversalExpression((expression ?? Expression.Empty()).Condition(ifTrue, ifFalse, type));

        /// <summary>
        /// Constructs conditional expression.
        /// </summary>
        /// <typeparam name="R">The type of conditional expression.</typeparam>
        /// <param name="ifTrue">Positive branch.</param>
        /// <param name="ifFalse">Negative branch.</param>
        /// <returns>Conditional expression.</returns>
        public UniversalExpression Condition<R>(Expression ifTrue, Expression ifFalse)
            => new UniversalExpression((expression ?? Expression.Empty()).Condition<R>(ifTrue, ifFalse));

        /// <summary>
        /// Constructs <c>throw</c> statement.
        /// </summary>
        /// <returns><c>throw</c> statement.</returns>
        public UnaryExpression Throw() => (expression ?? Expression.Empty()).Throw();

        /// <summary>
        /// Computes the hash code for the underlying expression.
        /// </summary>
        /// <returns>The hash code of the underlying expression.</returns>
        public override int GetHashCode() => expression is null ? 0 : expression.GetHashCode();

        /// <summary>
        /// Determines whether this universal expression
        /// represents the same underlying expression as other.
        /// </summary>
        /// <param name="other">Other expression to compare.</param>
        /// <returns><see langword="true"/>, if both expressions are equal; otherwise, <see langword="false"/>.</returns>
        public bool Equals(UniversalExpression other) => Equals(expression, other.expression);

        /// <summary>
        /// Determines whether this universal expression
        /// represents the same underlying expression as other.
        /// </summary>
        /// <param name="other">Other expression to compare.</param>
        /// <returns><see langword="true"/>, if both expressions are equal; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
        {
            switch (other)
            {
                case Expression expr:
                    return Equals(expression, expr);
                case UniversalExpression view:
                    return Equals(view);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns textual representation of this expression.
        /// </summary>
        /// <returns>The textual representation of this expression.</returns>
        public override string ToString() => expression?.ToString();

        Expression IExpressionBuilder<Expression>.Build() => expression;

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) => new MetaExpression(parameter, this);
    }
}
