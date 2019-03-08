using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Reflection;

    /// <summary>
    /// Represents any expression with full support
    /// of overloaded operators and conversion from
    /// primitive data types.
    /// </summary>
    /// <remarks>
    /// This class is intended for expression building purposes only.
    /// </remarks>
    public readonly struct UniversalExpression
    {
        private readonly Expression expression;

        /// <summary>
        /// Wraps regular LINQ expression into universal expression.
        /// </summary>
        /// <param name="expr">An expression to be wrapped.</param>
        public UniversalExpression(Expression expr) => expression = expr;

        internal static IEnumerable<Expression> AsExpressions(IEnumerable<UniversalExpression> expressions)
            => expressions.Select(Conversion<UniversalExpression, Expression>.Converter.AsFunc());

        internal static Expression[] AsExpressions(UniversalExpression[] expressions)
            => expressions.Convert(Conversion<UniversalExpression, Expression>.Converter);

        /// <summary>
        /// Converts universal expression into regular LINQ expression.
        /// </summary>
        /// <param name="view">Universal expression to be converted.</param>
        public static implicit operator Expression(UniversalExpression view) => view.expression ?? Expression.Empty();

        /// <summary>
        /// Converts universal expression into variable or parameter expression.
        /// </summary>
        /// <param name="view">Universal expression to be converted.</param>
        public static implicit operator ParameterExpression(UniversalExpression view) 
            => view.expression is ParameterExpression parameter ? parameter : throw new InvalidCastException(ExceptionMessages.ParameterExpected);

        /// <summary>
        /// Converts regular LINQ expression into universal expression.
        /// </summary>
        /// <param name="expr">Regular LINQ expression to be wrapped.</param>
        public static implicit operator UniversalExpression(Expression expr) => new UniversalExpression(expr);

        /// <summary>
        /// Constructs constant value of <see cref="long"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(long value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="ulong"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(ulong value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="int"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(int value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="uint"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(uint value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="short"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(short value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="ushort"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(ushort value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="byte"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(byte value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="sbyte"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(sbyte value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="bool"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(bool value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="string"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(string value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="decimal"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(decimal value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="float"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(float value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="double"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(double value) => value.AsConst();

        /// <summary>
        /// Logical NOT expression.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Constructed logical NOT expression.</returns>
        public static UniversalExpression operator !(UniversalExpression expr) => expr.expression.Not();

        /// <summary>
        /// Ones complement.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Ones complement expression.</returns>
        public static UniversalExpression operator ~(UniversalExpression expr) => expr.expression.OnesComplement();

        /// <summary>
        /// Unary plus.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UniversalExpression operator +(UniversalExpression expr) => expr.expression.UnaryPlus();
        
        /// <summary>
        /// Unary minus.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UniversalExpression operator -(UniversalExpression expr) => expr.expression.Negate();

        /// <summary>
        /// Binary arithmetic addition expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator +(UniversalExpression left, UniversalExpression right) => left.expression.Add(right);

        /// <summary>
        /// Binary arithmetic subtraction expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator -(UniversalExpression left, UniversalExpression right) => left.expression.Subtract(right);

        /// <summary>
        /// "greater than" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >(UniversalExpression left, UniversalExpression right) => left.expression.GreaterThan(right);

        /// <summary>
        /// "greater than or equal" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >=(UniversalExpression left, UniversalExpression right) => left.expression.GreaterThanOrEqual(right);

        /// <summary>
        /// "less than" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <(UniversalExpression left, UniversalExpression right) => left.expression.LessThan(right);

        /// <summary>
        /// "less than or equal" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <=(UniversalExpression left, UniversalExpression right) => left.expression.LessThanOrEqual(right);

        /// <summary>
        /// Binary arithmetic multiplication expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator *(UniversalExpression left, UniversalExpression right) => left.expression.Multiply(right);

        /// <summary>
        /// Binary arithmetic division expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator /(UniversalExpression left, UniversalExpression right) => left.expression.Divide(right);

        /// <summary>
        /// Binary logical OR expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator |(UniversalExpression left, UniversalExpression right) => left.expression.Or(right);

        /// <summary>
        /// Binary logical AND expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator &(UniversalExpression left, UniversalExpression right) => left.expression.Divide(right);

        /// <summary>
        /// Binary logical exclusive OR expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator ^(UniversalExpression left, UniversalExpression right) => left.expression.Xor(right);

        /// <summary>
        /// Equality comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator ==(UniversalExpression left, UniversalExpression right) => left.expression.Equal(right);

        /// <summary>
        /// Inequality comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator !=(UniversalExpression left, UniversalExpression right) => left.expression.NotEqual(right);

        /// <summary>
        /// Arithmetic remainder expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator %(UniversalExpression left, UniversalExpression right) => left.expression.Modulo(right);

        /// <summary>
        /// Bitwise right-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >>(UniversalExpression left, int right) => left.expression.RightShift(right.AsConst());

        /// <summary>
        /// Bitwise left-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <<(UniversalExpression left, int right) => left.expression.LeftShift(right.AsConst());

        /// <summary>
        /// Constructs raising a number to a power expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        public static UniversalExpression op_Exponent(UniversalExpression left, UniversalExpression right)
            => left.expression.Power(right);

        /// <summary>
        /// Bitwise left-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        public static UniversalExpression op_LeftShift(UniversalExpression left, UniversalExpression right)
            => left.expression.LeftShift(right);

        /// <summary>
        /// Bitwise right-shift expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        [SpecialName]
        public static UniversalExpression op_RightShift(UniversalExpression left, UniversalExpression right)
            => left.expression.RightShift(right);

        /// <summary>
        /// Constructs suspension point in the execution of the lambda function until the awaited task completes.
        /// </summary>
        /// <returns></returns>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await">Await expression</seealso>
        public UniversalExpression Await() => expression.Await();

        /// <summary>
        /// Constructs type conversion expression.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>The type conversion expression.</returns>
        public UniversalExpression Convert(Type targetType) => expression.Convert(targetType);

        /// <summary>
        /// Constructs type conversion expression.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The type conversion expression.</returns>
        public UniversalExpression Convert<T>() => expression.Convert<T>();

        /// <summary>
        /// Constructs type check expression.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>The type test expression.</returns>
        public UniversalExpression InstanceOf(Type targetType) => expression.InstanceOf(targetType);

        /// <summary>
        /// Constructs type check expression.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The type test expression.</returns>
        public UniversalExpression InstanceOf<T>() => expression.InstanceOf<T>();

        /// <summary>
        /// Constructs an expression that represents an explicit
        /// reference or boxing conversion where <see langword="null"/> is supplied if the conversion fails.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>Type conversion expression.</returns>
        public UniversalExpression TryConvert(Type targetType) => expression.TryConvert(targetType);

        /// <summary>
        /// Constructs an expression that represents an explicit
        /// reference or boxing conversion where <see langword="null"/> is supplied if the conversion fails.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>Type conversion expression.</returns>
        public UniversalExpression TryConvert<T>() => expression.TryConvert<T>();

        /// <summary>
        /// Constructs an expression that decrements this expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <returns>Unary expression.</returns>
        public UniversalExpression PreDecrementAssign() => expression.PreDecrementAssign();

        /// <summary>
        /// Constructs an expression that represents the assignment of this expression followed by a subsequent decrement by 1 of the original expression.
        /// </summary>
        /// <returns>Unary expression.</returns>
        public UniversalExpression PostDecrementAssign() => expression.PostDecrementAssign();

        /// <summary>
        /// Constructs an expression that increments this expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <returns>Unary expression.</returns>
        public UniversalExpression PreIncrementAssign() => expression.PreIncrementAssign();

        /// <summary>
        /// Constructs an expression that represents the assignment of this expression followed by a subsequent increment by 1 of the original expression.
        /// </summary>
        /// <returns>Unary expression.</returns>
        public UniversalExpression PostIncrementAssign() => expression.PostIncrementAssign();

        /// <summary>
        /// Binary expression that represents a conditional
        /// OR operation that evaluates the second operand only if the this expression evaluates to <see langword="false"/>.
        /// </summary>
        /// <param name="other">The second operand.</param>
        /// <returns>Binary expression.</returns>
        public UniversalExpression OrElse(Expression other) => expression.OrElse(other);

        /// <summary>
        /// Constructs binary expression that represents a conditional
        /// AND operation that evaluates the second operand only if the this expression evaluates to <see langword="true"/>.
        /// </summary>
        /// <param name="other">The second operand.</param>
        /// <returns>Binary expression.</returns>
        public UniversalExpression AndAlso(Expression other) => expression.AndAlso(other);

        /// <summary>
        /// Explicit unboxing.
        /// </summary>
        /// <param name="targetType">The target value type.</param>
        /// <returns>Unboxing expression.</returns>
        public UniversalExpression Unbox(Type targetType) => expression.Unbox(targetType);

        /// <summary>
        /// Explicit unboxing.
        /// </summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <returns>Unboxing expression.</returns>
        public UniversalExpression Unbox<T>()
            where T : struct
            => expression.Unbox<T>();

        /// <summary>
        /// Constructs delegate invocation expression.
        /// </summary>
        /// <param name="arguments">Invocation arguments.</param>
        /// <returns>Invocation expression.</returns>
        public InvocationExpression Invoke(params UniversalExpression[] arguments)
            => expression.Invoke(AsExpressions(arguments));

        /// <summary>
        /// Constructs array element access expression.
        /// </summary>
        /// <param name="indexes">Array element indicies.</param>
        /// <returns>Array element access expression.</returns>
        public UniversalExpression ElementAt(params UniversalExpression[] indexes) => expression.ElementAt(AsExpressions(indexes));

        /// <summary>
        /// Returns expression representing array length.
        /// </summary>
        /// <returns>Array length expression.</returns>
        public UniversalExpression ArrayLength() => expression.ArrayLength();

        /// <summary>
        /// Constructs instance method call expression.
        /// </summary>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public UniversalExpression Call(MethodInfo method, params UniversalExpression[] arguments) => expression.Call(method, AsExpressions(arguments));

        /// <summary>
        /// Constructs instance method call expression.
        /// </summary>
        /// <param name="methodName">The name of the method to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public UniversalExpression Call(string methodName, params UniversalExpression[] arguments) => expression.Call(methodName, AsExpressions(arguments));

        /// <summary>
        /// Constructs interface or base class method call expression.
        /// </summary>
        /// <param name="interfaceType">The interface or base class.</param>
        /// <param name="methodName">The name of the method in the interface or base class to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public UniversalExpression Call(Type interfaceType, string methodName, params UniversalExpression[] arguments) => expression.Call(interfaceType, methodName, AsExpressions(arguments));

        /// <summary>
        /// Constructs instance property or indexer access expression.
        /// </summary>
        /// <param name="property">Property metadata.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public UniversalExpression Property(PropertyInfo property, params UniversalExpression[] indicies) => expression.Property(property, AsExpressions(indicies));

        /// <summary>
        /// Constructs instance property or indexer access expression declared in the given interface or base type. 
        /// </summary>
        /// <param name="interfaceType">The interface or base class declaring property.</param>
        /// <param name="propertyName">The name of the instance property or indexer.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public UniversalExpression Property(Type interfaceType, string propertyName, params UniversalExpression[] indicies) => expression.Property(interfaceType, propertyName, AsExpressions(indicies));

        /// <summary>
        /// Constructs instance property or indexer access expression.
        /// </summary>
        /// <param name="propertyName">The name of the instance property or indexer.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public UniversalExpression Property(string propertyName, params UniversalExpression[] indicies) => expression.Property(propertyName, AsExpressions(indicies));

        /// <summary>
        /// Constructs instance field access expression.
        /// </summary>
        /// <param name="field">Field metadata.</param>
        /// <returns>Field access expression.</returns>
        public UniversalExpression Field(FieldInfo field) => expression.Field(field);

        /// <summary>
        /// Constructs instance field access expression.
        /// </summary>
        /// <param name="fieldName">The name of the instance field.</param>
        /// <returns>Field access expression.</returns>
        public UniversalExpression Field(string fieldName) => expression.Field(fieldName);

        /// <summary>
        /// Constructs loop statement which has a body equal to this expression.
        /// </summary>
        /// <param name="break">Optional loop break label which will installed automatically.</param>
        /// <param name="continue">Optional loop continuation which will be installed automatically.</param>
        /// <returns>Loop statement.</returns>
        public UniversalExpression Loop(LabelTarget @break, LabelTarget @continue) => expression.Loop(@break, @continue);

        /// <summary>
        /// Constructs loop statement which has a body equal to this expression.
        /// </summary>
        /// <param name="break">Optional loop break label which will installed automatically.</param>
        /// <returns>Loop statement.</returns>
        public UniversalExpression Loop(LabelTarget @break) => expression.Loop(@break);

        /// <summary>
        /// Constructs loop statement which has a body equal to this expression.
        /// </summary>
        /// <returns>Loop statement.</returns>
        public UniversalExpression Loop() => expression.Loop();

        /// <summary>
        /// Constructs conditional expression.
        /// </summary>
        /// <param name="ifTrue">Positive branch.</param>
        /// <param name="ifFalse">Negative branch.</param>
        /// <param name="type">The type of conditional expression. Default is <see langword="void"/>.</param>
        /// <returns>Conditional expression.</returns>
        public UniversalExpression Condition(Expression ifTrue = null, Expression ifFalse = null, Type type = null) 
            => expression.Condition(ifTrue, ifFalse, type);

        /// <summary>
        /// Constructs conditional expression.
        /// </summary>
        /// <typeparam name="R">The type of conditional expression.</typeparam>
        /// <param name="ifTrue">Positive branch.</param>
        /// <param name="ifFalse">Negative branch.</param>
        /// <returns>Conditional expression.</returns>
        public UniversalExpression Condition<R>(Expression ifTrue, Expression ifFalse)
            => expression.Condition<R>(ifTrue, ifFalse);

        /// <summary>
        /// Creates conditional expression builder.
        /// </summary>
        /// <param name="parent">Parent lexical scope.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Condition(CompoundStatementBuilder parent = null)
            => expression.Condition(parent);

        /// <summary>
        /// Constructs compound statement hat repeatedly refer to a single object or 
        /// structure so that the statements can use a simplified syntax when accessing members 
        /// of the object or structure.
        /// </summary>
        /// <param name="scope">The scope statements builder.</param>
        /// <param name="parent">Parent lexical scope.</param>
        /// <returns>Construct code block.</returns>
        /// <see cref="WithBlockBuilder"/>
        /// <see cref="WithBlockBuilder.ScopeVar"/>
        public UniversalExpression With(Action<WithBlockBuilder> scope, CompoundStatementBuilder parent = null) => expression.With(scope, parent);

        /// <summary>
        /// Constructs <see langword="using"/> statement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>using(var obj = expression){ }</code>.
        /// </remarks>
        /// <param name="scope">The body of <see langword="using"/> statement.</param>
        /// <param name="parent">Optional parent scope.</param>
        /// <returns><see langword="using"/> statement.</returns>
        public UniversalExpression Using(Action<UsingBlockBuilder> scope, CompoundStatementBuilder parent)
            => expression.Using(scope, parent);

        /// <summary>
        /// Creates selection statement builder that chooses a single <see langword="switch"/> section 
        /// to execute from a list of candidates based on a pattern match with the match expression.
        /// </summary>
        /// <param name="parent">Optional parent scope.</param>
        /// <returns><see langword="switch"/> statement builder.</returns>
        public SwitchBuilder Switch(CompoundStatementBuilder parent = null)
            => expression.Switch(parent);

        /// <summary>
        /// Creates structured exception handling statement builder.
        /// </summary>
        /// <param name="parent">The parent lexical scope.</param>
        /// <returns>Structured exception handling statement builder.</returns>
        public TryBuilder Try(CompoundStatementBuilder parent = null) => expression.Try(parent);

        /// <summary>
        /// Constructs <see langword="throw"/> statement.
        /// </summary>
        /// <returns><see langword="throw"/> statement.</returns>
        public UnaryExpression Throw() => expression.Throw();

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
        public override bool Equals(object other)
        {
            switch(other)
            {
                case Expression expr:
                    return Equals(expression, expr);
                case UniversalExpression view:
                    return Equals(expression, view.expression);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns textual representation of this expression.
        /// </summary>
        /// <returns>The textual representation of this expression.</returns>
        public override string ToString() => expression?.ToString();
    }
}
