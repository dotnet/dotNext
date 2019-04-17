using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Provides extension methods to simplify construction of complex expressions.
    /// </summary>
    public static class ExpressionBuilder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static E Build<E, B>(this B builder, Action<B> scope)
            where E : Expression
            where B: class, IExpressionBuilder<E>
        {
            scope(builder);
            return builder.Build();
        }

        /// <summary>
        /// Constructs unary plus expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>+a</code>
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression UnaryPlus(this Expression expression)
            => Expression.UnaryPlus(expression);

        /// <summary>
        /// Constructs negate expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>-a</code>
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression Negate(this Expression expression)
            => Expression.Negate(expression);

        /// <summary>
        /// Constructs logical NOT expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>!a</code>
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression Not(this Expression expression)
            => Expression.Not(expression);

        /// <summary>
        /// Constructs ones complement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>~a</code>
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression OnesComplement(this Expression expression)
            => Expression.OnesComplement(expression);

        /// <summary>
        /// Constructs binary logical AND expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a &amp; b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression And(this Expression left, Expression right)
            => Expression.And(left, right);

        /// <summary>
        /// Constructs binary logical OR expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a | b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Or(this Expression left, Expression right)
            => Expression.Or(left, right);

        /// <summary>
        /// Constructs binary logical exclusive OR expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a ^ b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Xor(this Expression left, Expression right)
            => Expression.ExclusiveOr(left, right);

        /// <summary>
        /// Constructs arithmetic remainder expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a % b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Modulo(this Expression left, Expression right)
            => Expression.Modulo(left, right);

        /// <summary>
        /// Constructs binary arithmetic addition expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a + b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Add(this Expression left, Expression right)
            => Expression.Add(left, right);

        /// <summary>
        /// Constructs binary arithmetic subtraction expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a - b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Subtract(this Expression left, Expression right)
            => Expression.Subtract(left, right);

        /// <summary>
        /// Constructs binary arithmetic multiplication expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a * b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Multiply(this Expression left, Expression right)
            => Expression.Multiply(left, right);

        /// <summary>
        /// Constructs binary arithmetic division expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a / b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Divide(this Expression left, Expression right)
            => Expression.Divide(left, right);

        /// <summary>
        /// Constructs "greater than" numeric comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a &gt; b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression GreaterThan(this Expression left, Expression right)
            => Expression.GreaterThan(left, right);

        /// <summary>
        /// Constructs "less than" numeric comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a &lt; b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression LessThan(this Expression left, Expression right)
            => Expression.LessThan(left, right);

        /// <summary>
        /// Constructs "greater than or equal" numeric comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a &gt;= b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression GreaterThanOrEqual(this Expression left, Expression right)
            => Expression.GreaterThanOrEqual(left, right);

        /// <summary>
        /// Constructs "less than or equal" numeric comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a &lt;= b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression LessThanOrEqual(this Expression left, Expression right)
            => Expression.LessThanOrEqual(left, right);

        /// <summary>
        /// Constructs equality comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a == b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Equal(this Expression left, Expression right)
            => Expression.Equal(left, right);

        /// <summary>
        /// Constructs inequality comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a != b</code>
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression NotEqual(this Expression left, Expression right)
            => Expression.NotEqual(left, right);

        /// <summary>
        /// Constructs <see langword="null"/> check.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a is null</code>
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns><see langword="null"/> check operation.</returns>
        public static BinaryExpression IsNull(this Expression operand)
            => Expression.ReferenceEqual(operand, Expression.Constant(null, operand.Type));

        /// <summary>
        /// Constructs raising a number to a power expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a ^ b</code> in Visual Basic.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Power(this Expression left, Expression right)
            => Expression.Power(left, right);

        /// <summary>
        /// Constructs bitwise left-shift expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a &lt;&lt; b</code> in Visual Basic.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression LeftShift(this Expression left, Expression right)
            => Expression.LeftShift(left, right);

        /// <summary>
        /// Constructs bitwise right-shift expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a &gt;&gt; b</code> in Visual Basic.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression RightShift(this Expression left, Expression right)
            => Expression.RightShift(left, right);

        /// <summary>
        /// Constructs an expression that decrements given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>--i</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreDecrementAssign(this ParameterExpression operand)
            => Expression.PreDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that increments given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>++i</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreIncrementAssign(this ParameterExpression operand)
            => Expression.PreIncrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent decrement by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>i--</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostDecrementAssign(this ParameterExpression operand)
            => Expression.PostDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent increment by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>i++</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostIncrementAssign(this ParameterExpression operand)
            => Expression.PostIncrementAssign(operand);
        
        /// <summary>
        /// Constructs an expression that decrements given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>--a.b</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreDecrementAssign(this MemberExpression operand)
            => Expression.PreDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that increments given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>++a.b</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreIncrementAssign(this MemberExpression operand)
            => Expression.PreIncrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent decrement by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b--</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostDecrementAssign(this MemberExpression operand)
            => Expression.PostDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent increment by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b++</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostIncrementAssign(this MemberExpression operand)
            => Expression.PostIncrementAssign(operand);
        
        /// <summary>
        /// Constructs an expression that decrements given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>--a.b[i]</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreDecrementAssign(this IndexExpression operand)
            => Expression.PreDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that increments given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>++a.b[i]</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreIncrementAssign(this IndexExpression operand)
            => Expression.PreIncrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent decrement by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b[i]--</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostDecrementAssign(this IndexExpression operand)
            => Expression.PostDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent increment by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b[i]++</code>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostIncrementAssign(this IndexExpression operand)
            => Expression.PostIncrementAssign(operand);

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a = b</code>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <param name="value">The value to be assigned to the left expression.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Assign(this ParameterExpression left, Expression value)
            => Expression.Assign(left, value);
        
        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b[i] = c</code>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <param name="value">The value to be assigned to the left expression.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Assign(this IndexExpression left, Expression value)
            => Expression.Assign(left, value);

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a = default(T)</code>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression AssignDefault(this ParameterExpression left)
            => left.Assign(left.Type.AsDefault());

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.member = default(T)</code>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression AssignDefault(this MemberExpression left)
            => left.Assign(left.Type.AsDefault());

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.member[i] = default(T)</code>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression AssignDefault(this IndexExpression left)
            => left.Assign(left.Type.AsDefault());

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.member = b</code>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <param name="value">The value to be assigned to the left expression.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Assign(this MemberExpression left, Expression value)
            => Expression.Assign(left, value);

        /// <summary>
        /// Constructs type conversion expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>(T)a</code>.
        /// </remarks>
        /// <param name="expression">The expression to be converted.</param>
        /// <param name="targetType">The target type.</param>
        /// <returns>The type conversion expression.</returns>
        public static UnaryExpression Convert(this Expression expression, Type targetType)
            => Expression.Convert(expression, targetType);

        /// <summary>
        /// Constructs type conversion expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>(T)a</code>.
        /// </remarks>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="expression">The expression to be converted.</param>
        /// <returns>The type conversion expression.</returns>
        public static UnaryExpression Convert<T>(this Expression expression)
            => expression.Convert(typeof(T));

        /// <summary>
        /// Constructs type check expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a is T</code>.
        /// </remarks>
        /// <param name="expression">The expression to test.</param>
        /// <param name="type">The target type.</param>
        /// <returns>The type test expression.</returns>
        public static TypeBinaryExpression InstanceOf(this Expression expression, Type type)
            => Expression.TypeIs(expression, type);

        /// <summary>
        /// Constructs type check expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a is T</code>.
        /// </remarks>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="expression">The expression to test.</param>
        /// <returns>The type test expression.</returns>
        public static TypeBinaryExpression InstanceOf<T>(this Expression expression)
            => expression.InstanceOf(typeof(T));

        /// <summary>
        /// Constructs an expression that represents an explicit
        /// reference or boxing conversion where <see langword="null"/> is supplied if the conversion fails.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a as T</code>.
        /// </remarks>
        /// <param name="expression">The expression to convert.</param>
        /// <param name="type">The target type.</param>
        /// <returns>Type conversion expression.</returns>
        public static UnaryExpression TryConvert(this Expression expression, Type type)
            => Expression.TypeAs(expression, type);

        /// <summary>
        /// Constructs an expression that represents an explicit
        /// reference or boxing conversion where <see langword="null"/> is supplied if the conversion fails.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a as T</code>.
        /// </remarks>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="expression">The expression to convert.</param>
        /// <returns>Type conversion expression.</returns>
        public static UnaryExpression TryConvert<T>(this Expression expression)
            => expression.TryConvert(typeof(T));

        /// <summary>
        /// Constructs binary expression that represents a conditional
        /// AND operation that evaluates the second operand only if the first operand evaluates to <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a &amp;&amp; b</code>.
        /// </remarks>
        /// <param name="left">The first operand.</param>
        /// <param name="right">The second operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression AndAlso(this Expression left, Expression right)
            => Expression.AndAlso(left, right);

        /// <summary>
        /// Constructs binary expression that represents a conditional
        /// OR operation that evaluates the second operand only if the first operand evaluates to <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a || b</code>.
        /// </remarks>
        /// <param name="left">The first operand.</param>
        /// <param name="right">The second operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression OrElse(this Expression left, Expression right)
            => Expression.OrElse(left, right);

        /// <summary>
        /// Constructs suspension point in the execution of the lambda function until the awaited task completes.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>await b</c>.
        /// </remarks>
        /// <param name="expression">The expression </param>
        /// <returns><see langword="await"/> expression.</returns>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await">Await expression</seealso>
        public static AwaitExpression Await(this Expression expression)
            => new AwaitExpression(expression);

        /// <summary>
        /// Constructs explicit unboxing.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>(T)b</code>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <param name="type">The target value type.</param>
        /// <returns>Unboxing expression.</returns>
        public static UnaryExpression Unbox(this Expression expression, Type type)
            => Expression.Unbox(expression, type);

        /// <summary>
        /// Constructs explicit unboxing.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>(T)b</code>.
        /// </remarks>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="expression">The operand.</param>
        /// <returns>Unboxing expression.</returns>
        public static UnaryExpression Unbox<T>(this Expression expression)
            where T : struct
            => expression.Unbox(typeof(T));

        /// <summary>
        /// Constructs delegate invocation expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>delegate.Invoke(a, b,...)</code>.
        /// </remarks>
        /// <param name="delegate">The expression representing delegate.</param>
        /// <param name="arguments">Invocation arguments.</param>
        /// <returns>Invocation expression.</returns>
        public static InvocationExpression Invoke(this Expression @delegate, params Expression[] arguments)
            => Expression.Invoke(@delegate, arguments);

        /// <summary>
        /// Constructs instance method call expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>obj.Method(a, b,...)</code>.
        /// </remarks>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public static MethodCallExpression Call(this Expression instance, MethodInfo method, params Expression[] arguments)
            => Expression.Call(instance, method, arguments);

        /// <summary>
        /// Constructs instance method call expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>obj.Method()</code>.
        /// </remarks>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="methodName">The name of the method to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public static MethodCallExpression Call(this Expression instance, string methodName, params Expression[] arguments)
            => instance.Call(instance.Type, methodName, arguments);

        /// <summary>
        /// Constructs interface or base class method call expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>((T)obj).Method()</code>.
        /// </remarks>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="interfaceType">The interface or base class.</param>
        /// <param name="methodName">The name of the method in the interface or base class to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public static MethodCallExpression Call(this Expression instance, Type interfaceType, string methodName, params Expression[] arguments)
        {
            if (!interfaceType.IsAssignableFrom(instance.Type))
                throw new ArgumentException(ExceptionMessages.InterfaceNotImplemented(instance.Type, interfaceType));
            var method = interfaceType.GetMethod(methodName, Array.ConvertAll(arguments, arg => arg.Type));
            return method is null ?
                throw new MissingMethodException(ExceptionMessages.MissingMethod(methodName, interfaceType)) :
                instance.Call(method, arguments);
        }

        /// <summary>
        /// Constructs instance property or indexer access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b</code> or <code>a.b[i]</code>.
        /// </remarks>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="property">Property metadata.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public static Expression Property(this Expression instance, PropertyInfo property, params Expression[] indicies)
            => indicies.LongLength == 0 ? (Expression)Expression.Property(instance, property) : Expression.Property(instance, property, indicies);

        /// <summary>
        /// Constructs instance property or indexer access expression declared in the given interface or base type. 
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b</code> or <code>a.b[i]</code>.
        /// </remarks>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="interfaceType">The interface or base class declaring property.</param>
        /// <param name="propertyName">The name of the instance property or indexer.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public static Expression Property(this Expression instance, Type interfaceType, string propertyName, params Expression[] indicies)
        {
            var property = interfaceType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            return property is null ?
                throw new MissingMemberException(ExceptionMessages.MissingProperty(propertyName, interfaceType)) :
                instance.Property(property, indicies);
        }

        /// <summary>
        /// Constructs instance property or indexer access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b</code> or <code>a.b[i]</code>.
        /// </remarks>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="propertyName">The name of the instance property or indexer.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public static Expression Property(this Expression instance, string propertyName, params Expression[] indicies)
            => Expression.Property(instance, propertyName, indicies);

        /// <summary>
        /// Constructs instance field access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b</code>.
        /// </remarks>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="field">Field metadata.</param>
        /// <returns>Field access expression.</returns>
        public static MemberExpression Field(this Expression instance, FieldInfo field)
            => Expression.Field(instance, field);

        /// <summary>
        /// Constructs instance field access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b</code>.
        /// </remarks>
        /// <param name="instance"><see langword="this"/> argument.</param>
        /// <param name="fieldName">The name of the instance field.</param>
        /// <returns>Field access expression.</returns>
        public static MemberExpression Field(this Expression instance, string fieldName)
            => Expression.Field(instance, fieldName);

        /// <summary>
        /// Constructs array element access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.b[i]</code>.
        /// </remarks>
        /// <param name="array">The array expression.</param>
        /// <param name="indexes">Array element indicies.</param>
        /// <returns>Array element access expression.</returns>
        public static IndexExpression ElementAt(this Expression array, params Expression[] indexes)
            => Expression.ArrayAccess(array, indexes);

        /// <summary>
        /// Constructs array length expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a.LongLength</code>.
        /// </remarks>
        /// <param name="array">The array expression.</param>
        /// <returns>Array length expression.</returns>
        public static UnaryExpression ArrayLength(this Expression array)
            => Expression.ArrayLength(array);

        /// <summary>
        /// Constructs loop statement.
        /// </summary>
        /// <param name="body">The loop body.</param>
        /// <param name="break">Optional loop break label which will installed automatically.</param>
        /// <param name="continue">Optional loop continuation which will be installed automatically.</param>
        /// <returns>Loop statement.</returns>
        public static LoopExpression Loop(this Expression body, LabelTarget @break, LabelTarget @continue)
            => Expression.Loop(body, @break, @continue);

        /// <summary>
        /// Constructs loop statement.
        /// </summary>
        /// <param name="body">The loop body.</param>
        /// <param name="break">Optional loop break label which will installed automatically.</param>
        /// <returns>Loop statement.</returns>
        public static LoopExpression Loop(this Expression body, LabelTarget @break) => Expression.Loop(body, @break);

        /// <summary>
        /// Constructs loop statement.
        /// </summary>
        /// <param name="body">The loop body.</param>
        /// <returns>Loop statement.</returns>
        public static LoopExpression Loop(this Expression body) => Expression.Loop(body);

        /// <summary>
        /// Constructs unconditional control transfer statement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>goto label</code>.
        /// </remarks>
        /// <param name="label">The declared label.</param>
        /// <returns>Unconditional control transfer statement.</returns>
        public static GotoExpression Goto(this LabelTarget label) => Expression.Goto(label);

        /// <summary>
        /// Constructs unconditional control transfer expression.
        /// </summary>
        /// <param name="label">The declared label.</param>
        /// <param name="value"></param>
        /// <returns>Unconditional control transfer expression.</returns>
        public static GotoExpression Goto(this LabelTarget label, Expression value) => Expression.Goto(label, value);

        /// <summary>
        /// Constructs <see langword="return"/> statement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>return</code>.
        /// </remarks>
        /// <param name="label">The label representing function exit.</param>
        /// <returns>Return statement.</returns>
        public static GotoExpression Return(this LabelTarget label) => Expression.Return(label);

        /// <summary>
        /// Constructs <see langword="return"/> statement with given value.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>return a</code>.
        /// </remarks>
        /// <param name="label">The label representing function exit.</param>
        /// <param name="value">The value to be returned from function.</param>
        /// <returns>Return statement.</returns>
        public static GotoExpression Return(this LabelTarget label, Expression value) => Expression.Return(label, value);

        /// <summary>
        /// Constructs loop leave statement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>break</code>.
        /// </remarks>
        /// <param name="label">The label indicating loop exit.</param>
        /// <returns>Break statement.</returns>
        public static GotoExpression Break(this LabelTarget label) => Expression.Break(label);

        /// <summary>
        /// Constructs loop leave statement.
        /// </summary>
        /// <param name="label">The label indicating loop exit.</param>
        /// <param name="value">The value to be returned from loop.</param>
        /// <returns>Break statement.</returns>
        public static GotoExpression Break(this LabelTarget label, Expression value) => Expression.Break(label, value);

        /// <summary>
        /// Constructs loop continuation statement.
        /// </summary>
        /// <param name="label">The label indicating loop start.</param>
        /// <returns>Continue statement.</returns>
        public static GotoExpression Continue(this LabelTarget label) => Expression.Continue(label);

        /// <summary>
        /// Constructs label landing site.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>label:</code>.
        /// </remarks>
        /// <param name="label">The label reference.</param>
        /// <returns>The label landing site.</returns>
        public static LabelExpression LandingSite(this LabelTarget label) => Expression.Label(label);

        /// <summary>
        /// Constructs label landing site with the default value.
        /// </summary>
        /// <param name="label">The label reference.</param>
        /// <param name="default">The default value associated with the label.</param>
        /// <returns>The label landing site.</returns>
        public static LabelExpression LandingSite(this LabelTarget label, Expression @default) => Expression.Label(label, @default);

        /// <summary>
        /// Constructs conditional expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a ? b : c</code>.
        /// </remarks>
        /// <param name="test">Test expression.</param>
        /// <param name="ifTrue">Positive branch.</param>
        /// <param name="ifFalse">Negative branch.</param>
        /// <param name="type">The type of conditional expression. Default is <see langword="void"/>.</param>
        /// <returns>Conditional expression.</returns>
        public static ConditionalExpression Condition(this Expression test, Expression ifTrue = null, Expression ifFalse = null, Type type = null)
            => Expression.Condition(test, ifTrue ?? Expression.Empty(), ifFalse ?? Expression.Empty(), type ?? typeof(void));

        /// <summary>
        /// Constructs conditional expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>a ? b : c</code>.
        /// </remarks>
        /// <typeparam name="R">The type of conditional expression. Default is <see langword="void"/>.</typeparam>
        /// <param name="test">Test expression.</param>
        /// <param name="ifTrue">Positive branch.</param>
        /// <param name="ifFalse">Negative branch.</param>
        /// <returns>Conditional expression.</returns>
        public static ConditionalExpression Condition<R>(this Expression test, Expression ifTrue, Expression ifFalse)
            => test.Condition(ifTrue, ifFalse, typeof(R));

        /// <summary>
        /// Creates conditional expression builder.
        /// </summary>
        /// <param name="test">Test expression.</param>
        /// <param name="parent">Parent lexical scope.</param>
        /// <returns>Conditional expression builder.</returns>
        public static ConditionalBuilder Condition(this Expression test, CompoundStatementBuilder parent = null)
            => new ConditionalBuilder(test, parent, false);

        /// <summary>
        /// Constructs a <see langword="try"/> block with a <see langword="finally"/> block without <see langword="catch"/> block.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>try { } finally { }</code>.
        /// </remarks>
        /// <param name="try"><see langword="try"/> block.</param>
        /// <param name="finally"><see langword="finally"/> block</param>
        /// <returns>Try-finally statement.</returns>
        public static TryExpression Finally(this Expression @try, Expression @finally) => Expression.TryFinally(@try, @finally);

        /// <summary>
        /// Constructs <see langword="throw"/> expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>throw e</code>.
        /// </remarks>
        /// <param name="exception">An exception to be thrown.</param>
        /// <param name="type">The type of expression. Default is <see langword="void"/>.</param>
        /// <returns><see langword="throw"/> expression.</returns>
        public static UnaryExpression Throw(this Expression exception, Type type = null) => Expression.Throw(exception, type ?? typeof(void));

        /// <summary>
        /// Converts arbitrary value into constant expression.
        /// </summary>
        /// <typeparam name="T">The type of constant.</typeparam>
        /// <param name="value">The constant value.</param>
        /// <returns></returns>
        public static ConstantExpression AsConst<T>(this T value) => Expression.Constant(value, typeof(T));

        /// <summary>
        /// Constructs type default value supplier.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>default(T)</code>.
        /// </remarks>
        /// <param name="type">The target type.</param>
        /// <returns>The type default value expression.</returns>
        public static DefaultExpression AsDefault(this Type type) => Expression.Default(type);

        /// <summary>
        /// Constructs type instantiation expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>new T()</code>.
        /// </remarks>
        /// <param name="type">The type to be instantiated.</param>
        /// <param name="args">The list of arguments to be passed into constructor.</param>
        /// <returns>Instantiation expression.</returns>
        public static NewExpression New(this Type type, params Expression[] args)
        {
            if (args.LongLength == 0L)
                return Expression.New(type);
            var ctor = type.GetConstructor(Array.ConvertAll(args, arg => arg.Type));
            if (ctor is null)
                throw new MissingMethodException(ExceptionMessages.MissingCtor(type));
            else
                return Expression.New(ctor, args);
        }

        /// <summary>
        /// Creates structured exception handling statement builder.
        /// </summary>
        /// <param name="expression"><see langword="try"/> block.</param>
        /// <param name="parent">The parent lexical scope.</param>
        /// <returns>Structured exception handling statement builder.</returns>
        public static TryBuilder Try(this Expression expression, CompoundStatementBuilder parent = null)
            => new TryBuilder(expression, parent, false);

        /// <summary>
        /// Constructs compound statement hat repeatedly refer to a single object or 
        /// structure so that the statements can use a simplified syntax when accessing members 
        /// of the object or structure.
        /// </summary>
        /// <param name="expression">An expression to be captured by scope.</param>
        /// <param name="scope">The scope statements builder.</param>
        /// <param name="parent">Parent lexical scope.</param>
        /// <returns>Construct code block.</returns>
        /// <see cref="WithBlockBuilder"/>
        /// <see cref="WithBlockBuilder.ScopeVar"/>
        public static Expression With(this Expression expression, Action<WithBlockBuilder> scope, CompoundStatementBuilder parent = null)
        {
            using(var builder = new WithBlockBuilder(expression, parent))
                return builder.Build<Expression, WithBlockBuilder>(scope);
        }

        /// <summary>
        /// Constructs <see langword="using"/> statement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>using(var obj = expression){ }</code>.
        /// </remarks>
        /// <param name="expression">The expression representing disposable resource.</param>
        /// <param name="scope">The body of <see langword="using"/> statement.</param>
        /// <param name="parent">Optional parent scope.</param>
        /// <returns><see langword="using"/> statement.</returns>
        public static Expression Using(this Expression expression, Action<UsingBlockBuilder> scope, CompoundStatementBuilder parent = null)
        {
            using(var builder = new UsingBlockBuilder(expression, parent))
                return builder.Build<Expression, UsingBlockBuilder>(scope);
        }

        /// <summary>
        /// Creates selection statement builder that chooses a single <see langword="switch"/> section 
        /// to execute from a list of candidates based on a pattern match with the match expression.
        /// </summary>
        /// <param name="switchValue">The value to be matched with provided candidates.</param>
        /// <param name="parent">Optional parent scope.</param>
        /// <returns><see langword="switch"/> statement builder.</returns>
        public static SwitchBuilder Switch(this Expression switchValue, CompoundStatementBuilder parent = null)
            => new SwitchBuilder(switchValue, parent, false);

        /// <summary>
        /// Transforms async lambda function into read-to-compile function.
        /// </summary>
        /// <typeparam name="D">Type of the delegate describing signature of asynchronous function.</typeparam>
        /// <param name="lambda">The lambda with <see langword="await"/> expressions.</param>
        /// <returns>Prepared async lambda function.</returns>
        /// <see cref="AsyncResultExpression"/>
        /// <see cref="AwaitExpression"/>
        public static Expression<D> ToAsyncLambda<D>(this Expression<D> lambda)
            where D : Delegate
        {
            using (var builder = new Runtime.CompilerServices.AsyncStateMachineBuilder<D>(lambda.Parameters))
                return builder.Build(lambda.Body, lambda.TailCall);
        }

        internal static Expression AddPrologue(this Expression expression, bool inferType, IReadOnlyCollection<Expression> instructions)
        {
            if (instructions.Count == 0)
                return expression;
            else if (expression is BlockExpression block)
                return Expression.Block(inferType ? block.Type : typeof(void), block.Variables, instructions.Concat(block.Expressions));
            else
                return Expression.Block(inferType ? expression.Type : typeof(void), instructions.Concat(Sequence.Singleton(expression)));
        }

        internal static Expression AddEpilogue(this Expression expression, bool inferType, IReadOnlyCollection<Expression> instructions)
        {
            if (instructions.Count == 0)
                return expression;
            else if (expression is BlockExpression block)
                return Expression.Block(inferType ? block.Type : typeof(void), block.Variables, block.Expressions.Concat(instructions));
            else
                return Expression.Block(inferType ? instructions.Last().Type : typeof(void), Sequence.Singleton(expression).Concat(instructions));
        }

        internal static Expression AddPrologue(this Expression expression, bool inferType, params Expression[] instructions)
            => AddPrologue(expression, inferType, (IReadOnlyCollection<Expression>)instructions);

        internal static Expression AddEpilogue(this Expression expression, bool inferType, params Expression[] instructions)
            => AddEpilogue(expression, inferType, (IReadOnlyCollection<Expression>)instructions);

        /// <summary>
        /// Constructs type instantiation expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <code>new T()</code>.
        /// </remarks>
        /// <param name="type">The expression representung the type to be instantiated.</param>
        /// <param name="args">The list of arguments to be passed into constructor.</param>
        /// <returns>Instantiation expression.</returns>
        public static MethodCallExpression New(this Expression type, params Expression[] args)
        {
            var activate = typeof(Activator).GetMethod(nameof(Activator.CreateInstance), new[] { typeof(Type), typeof(object[]) });
            return Expression.Call(activate, type, Expression.NewArrayInit(typeof(object), args));
        }
    }

    /// <summary>
    /// Represents compound expresssion builder.
    /// </summary>
    /// <typeparam name="E">Type of expression to be constructed.</typeparam>
    public abstract class ExpressionBuilder<E> : IExpressionBuilder<E>
        where E : Expression
    {
        private readonly CompoundStatementBuilder parent;
        private readonly bool treatAsStatement;
        private Type expressionType;

        private protected ExpressionBuilder(CompoundStatementBuilder parent, bool treatAsStatement)
        {
            this.parent = parent;
            this.treatAsStatement = treatAsStatement;
        }

        private protected ScopeBuilder NewScope() => NewScope(parent => new ScopeBuilder(parent));

        private protected B NewScope<B>(Func<CompoundStatementBuilder, B> factory)
            where B : ScopeBuilder
            => factory(parent);

        private protected Type ExpressionType => expressionType ?? typeof(void);

        /// <summary>
        /// Changes type of the expression.
        /// </summary>
        /// <remarks>
        /// By default, type of expression is <see cref="void"/>.
        /// </remarks>
        /// <param name="expressionType">The expression type.</param>
        /// <returns>This builder.</returns>
        public ExpressionBuilder<E> OfType(Type expressionType)
        {
            this.expressionType = expressionType;
            return this;
        }

        /// <summary>
        /// Changes type of the expression.
        /// </summary>
        /// <typeparam name="T">The expression type.</typeparam>
        /// <returns>This builder.</returns>
        public ExpressionBuilder<E> OfType<T>() => OfType(typeof(T));

        /// <summary>
        /// Constructs expression and, optionally, adds it to the underlying compound statement.
        /// </summary>
        /// <returns>Constructed expression.</returns>
        public E End()
        {
            var expr = Build();
            if (treatAsStatement)
                parent.AddStatement(expr);
            return expr;
        }

        private protected abstract E Build();

        E IExpressionBuilder<E>.Build() => Build();
    }
}