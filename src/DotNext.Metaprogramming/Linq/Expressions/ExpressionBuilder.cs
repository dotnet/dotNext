using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using static System.Linq.Enumerable;

namespace DotNext.Linq.Expressions
{
    using Reflection;
    using static Reflection.TypeExtensions;

    /// <summary>
    /// Provides extension methods to simplify construction of complex expressions.
    /// </summary>
    public static partial class ExpressionBuilder
    {
        /// <summary>
        /// Constructs unary plus expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>+a</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression UnaryPlus(this Expression expression)
            => Expression.UnaryPlus(expression);

        /// <summary>
        /// Constructs negate expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>-a</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression Negate(this Expression expression)
            => Expression.Negate(expression);

        /// <summary>
        /// Constructs logical NOT expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>!a</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression Not(this Expression expression)
            => Expression.Not(expression);

        /// <summary>
        /// Constructs ones complement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>~a</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression OnesComplement(this Expression expression)
            => Expression.OnesComplement(expression);

        /// <summary>
        /// Constructs binary logical AND expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &amp; b</c>.
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
        /// The equivalent code is <c>a | b</c>.
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
        /// The equivalent code is <c>a ^ b</c>.
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
        /// The equivalent code is <c>a % b</c>.
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
        /// The equivalent code is <c>a + b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression Add(this Expression left, Expression right)
            => Expression.Add(left, right);

        private static MethodCallExpression Concat(Expression[] strings) => strings.LongLength switch
        {
            2 => CallStatic(typeof(string), nameof(string.Concat), strings[0], strings[1]),
            3 => CallStatic(typeof(string), nameof(string.Concat), strings[0], strings[1], strings[2]),
            4 => CallStatic(typeof(string), nameof(string.Concat), strings[0], strings[1], strings[2], strings[3]),
            _ => CallStatic(typeof(string), nameof(string.Concat), Expression.NewArrayInit(typeof(string), strings)),
        };

        /// <summary>
        /// Constructs string concatenation expression.
        /// </summary>
        /// <param name="first">The first string to concatenate.</param>
        /// <param name="other">Other strings to concatenate.</param>
        /// <returns>An expression presenting concatenation.</returns>
        public static MethodCallExpression Concat(this Expression first, params Expression[] other)
            => Concat(other.Insert(first, 0L));

        /// <summary>
        /// Constructs binary arithmetic subtraction expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a - b</c>.
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
        /// The equivalent code is <c>a * b</c>.
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
        /// The equivalent code is <c>a / b</c>.
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
        /// The equivalent code is <c>a &gt; b</c>.
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
        /// The equivalent code is <c>a &lt; b</c>.
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
        /// The equivalent code is <c>a &gt;= b</c>.
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
        /// The equivalent code is <c>a &lt;= b</c>.
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
        /// The equivalent code is <c>a == b</c>.
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
        /// The equivalent code is <c>a != b</c>.
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
        /// The equivalent code is <c>a is null</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns><see langword="null"/> check operation.</returns>
        public static Expression IsNull(this Expression operand)
        {
            // handle nullable value type
            Type? underlyingType = Nullable.GetUnderlyingType(operand.Type);
            if (underlyingType is not null)
                return operand.Property(nameof(Nullable<int>.HasValue)).Not();

            // handle optional type
            underlyingType = Optional.GetUnderlyingType(operand.Type);
            if (underlyingType is not null)
                return operand.Property(nameof(Optional<int>.HasValue)).Not();

            // handle reference type or value type
            return operand.Type.IsValueType || operand.Type.IsPointer ? Const<bool>(false) : Expression.ReferenceEqual(operand, Expression.Constant(null, operand.Type));
        }

        /// <summary>
        /// Constructs <see langword="null"/> check.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>!(a is null)</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns><see langword="null"/> check operation.</returns>
        public static Expression IsNotNull(this Expression operand)
        {
            // handle nullable value type
            Type? underlyingType = Nullable.GetUnderlyingType(operand.Type);
            if (underlyingType is not null)
                return operand.Property(nameof(Nullable<int>.HasValue));

            // handle optional type
            underlyingType = Optional.GetUnderlyingType(operand.Type);
            if (underlyingType is not null)
                return operand.Property(nameof(Optional<int>.HasValue));

            // handle reference type or value type
            return operand.Type.IsValueType || operand.Type.IsPointer ? Const<bool>(true) : Expression.ReferenceNotEqual(operand, Expression.Constant(null, operand.Type));
        }

        /// <summary>
        /// Constructs raising a number to a power expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a ^ b</c> in Visual Basic.
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
        /// The equivalent code is <c>a &lt;&lt; b</c> in Visual Basic.
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
        /// The equivalent code is <c>a &gt;&gt; b</c> in Visual Basic.
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
        /// The equivalent code is <c>--i</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreDecrementAssign(this ParameterExpression operand)
            => Expression.PreDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that increments given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>++i</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreIncrementAssign(this ParameterExpression operand)
            => Expression.PreIncrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent decrement by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>i--</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostDecrementAssign(this ParameterExpression operand)
            => Expression.PostDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent increment by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>i++</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostIncrementAssign(this ParameterExpression operand)
            => Expression.PostIncrementAssign(operand);

        /// <summary>
        /// Constructs an expression that decrements given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>--a.b</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreDecrementAssign(this MemberExpression operand)
            => Expression.PreDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that increments given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>++a.b</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreIncrementAssign(this MemberExpression operand)
            => Expression.PreIncrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent decrement by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b--</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostDecrementAssign(this MemberExpression operand)
            => Expression.PostDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent increment by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b++</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostIncrementAssign(this MemberExpression operand)
            => Expression.PostIncrementAssign(operand);

        /// <summary>
        /// Constructs an expression that decrements given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>--a.b[i]</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreDecrementAssign(this IndexExpression operand)
            => Expression.PreDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that increments given expression by 1 and assigns the result back to the expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>++a.b[i]</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PreIncrementAssign(this IndexExpression operand)
            => Expression.PreIncrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent decrement by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b[i]--</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostDecrementAssign(this IndexExpression operand)
            => Expression.PostDecrementAssign(operand);

        /// <summary>
        /// Constructs an expression that represents the assignment of given expression followed by a subsequent increment by 1 of the original expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b[i]++</c>.
        /// </remarks>
        /// <param name="operand">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression PostIncrementAssign(this IndexExpression operand)
            => Expression.PostIncrementAssign(operand);

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a = b</c>.
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
        /// The equivalent code is <c>a.b[i] = c</c>.
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
        /// The equivalent code is <c>a = default(T)</c>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression AssignDefault(this ParameterExpression left)
            => left.Assign(left.Type.Default());

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.member = default(T)</c>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression AssignDefault(this MemberExpression left)
            => left.Assign(left.Type.Default());

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.member[i] = default(T)</c>.
        /// </remarks>
        /// <param name="left">The assignee.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression AssignDefault(this IndexExpression left)
            => left.Assign(left.Type.Default());

        /// <summary>
        /// Constructs assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.member = b</c>.
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
        /// The equivalent code is <c>(T)a</c>.
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
        /// The equivalent code is <c>(T)a</c>.
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
        /// The equivalent code is <c>a is T</c>.
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
        /// The equivalent code is <c>a is T</c>.
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
        /// The equivalent code is <c>a as T</c>.
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
        /// The equivalent code is <c>a as T</c>.
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
        /// The equivalent code is <c>a &amp;&amp; b</c>.
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
        /// The equivalent code is <c>a || b</c>.
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
        /// <param name="expression">The awaitable expression.</param>
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="System.Threading.Tasks.Task.ConfigureAwait(bool)"/> with <see langword="false"/> argument.</param>
        /// <returns><see langword="await"/> expression.</returns>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await">Await expression</seealso>
        public static AwaitExpression Await(this Expression expression, bool configureAwait = false)
            => new(expression, configureAwait);

        /// <summary>
        /// Constructs explicit unboxing.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>(T)b</c>.
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
        /// The equivalent code is <c>(T)b</c>.
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
        /// The equivalent code is <c>delegate.Invoke(a, b,...)</c>.
        /// </remarks>
        /// <param name="delegate">The expression representing delegate.</param>
        /// <param name="arguments">Invocation arguments.</param>
        /// <returns>Invocation expression.</returns>
        public static InvocationExpression Invoke(this Expression @delegate, params Expression[] arguments)
            => Expression.Invoke(@delegate, arguments);

        /// <summary>
        /// Extracts body of lambda expression.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate describing lambda call site.</typeparam>
        /// <param name="lambda">The lambda expression.</param>
        /// <param name="arguments">The arguments used to replace lambda parameters.</param>
        /// <returns>The body of lambda expression.</returns>
        public static Expression Fragment<TDelegate>(Expression<TDelegate> lambda, params Expression[] arguments)
            where TDelegate : MulticastDelegate
        {
            if (lambda.Parameters.Count != arguments.LongLength)
                throw new ArgumentException(ExceptionMessages.InvalidFragmentRendering);
            var replacer = new Runtime.CompilerServices.Replacer();
            for (var i = 0; i < arguments.Length; i++)
                replacer.Replace(lambda.Parameters[i], arguments[i]);
            return replacer.Visit(lambda.Body);
        }

        /// <summary>
        /// Constructs instance method call expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>obj.Method(a, b,...)</c>.
        /// </remarks>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="method">The method to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public static MethodCallExpression Call(this Expression instance, MethodInfo method, params Expression[] arguments)
            => Expression.Call(instance, method, arguments);

        /// <summary>
        /// Constructs instance method call expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>obj.Method()</c>.
        /// </remarks>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="methodName">The name of the method to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public static MethodCallExpression Call(this Expression instance, string methodName, params Expression[] arguments)
            => instance.Call(instance.Type, methodName, arguments);

        private static Type GetType(Expression expr) => expr.Type;

        /// <summary>
        /// Constructs interface or base class method call expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>((T)obj).Method()</c>.
        /// </remarks>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="interfaceType">The interface or base class.</param>
        /// <param name="methodName">The name of the method in the interface or base class to be called.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <returns>The method call expression.</returns>
        public static MethodCallExpression Call(this Expression instance, Type interfaceType, string methodName, params Expression[] arguments)
        {
            if (!interfaceType.IsAssignableFrom(instance.Type))
                throw new ArgumentException(ExceptionMessages.InterfaceNotImplemented(instance.Type, interfaceType));
            MethodInfo? method = interfaceType.GetMethod(methodName, Array.ConvertAll(arguments, GetType));
            return method is null ?
                throw new MissingMethodException(interfaceType.FullName, methodName) :
                instance.Call(method, arguments);
        }

        /// <summary>
        /// Constructs static method call.
        /// </summary>
        /// <param name="type">The type that declares static method.</param>
        /// <param name="methodName">The name of the static method.</param>
        /// <param name="arguments">The arguments to be passed into static method.</param>
        /// <returns>An expression representing static method call.</returns>
        public static MethodCallExpression CallStatic(this Type type, string methodName, params Expression[] arguments)
        {
            MethodInfo? method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly, null, Array.ConvertAll(arguments, GetType), null);
            return method is null ?
                throw new MissingMethodException(type.FullName, methodName) :
                Expression.Call(method, arguments);
        }

        /// <summary>
        /// Constructs instance property or indexer access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b</c> or <c>a.b[i]</c>.
        /// </remarks>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="property">Property metadata.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public static Expression Property(this Expression instance, PropertyInfo property, params Expression[] indicies)
            => indicies.LongLength == 0 ? Expression.Property(instance, property) : Expression.Property(instance, property, indicies);

        /// <summary>
        /// Constructs instance property or indexer access expression declared in the given interface or base type.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b</c> or <c>a.b[i]</c>.
        /// </remarks>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="interfaceType">The interface or base class declaring property.</param>
        /// <param name="propertyName">The name of the instance property or indexer.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public static Expression Property(this Expression instance, Type interfaceType, string propertyName, params Expression[] indicies)
        {
            PropertyInfo? property = interfaceType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property is null ?
                throw new MissingMemberException(interfaceType.FullName, propertyName) :
                instance.Property(property, indicies);
        }

        /// <summary>
        /// Constructs instance property or indexer access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b</c> or <c>a.b[i]</c>.
        /// </remarks>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="propertyName">The name of the instance property or indexer.</param>
        /// <param name="indicies">Indexer indicies.</param>
        /// <returns>Property access expression.</returns>
        public static Expression Property(this Expression instance, string propertyName, params Expression[] indicies)
            => Expression.Property(instance, propertyName, indicies);

        /// <summary>
        /// Constructs instance field access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b</c>.
        /// </remarks>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="field">Field metadata.</param>
        /// <returns>Field access expression.</returns>
        public static MemberExpression Field(this Expression instance, FieldInfo field)
            => Expression.Field(instance, field);

        /// <summary>
        /// Constructs instance field access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b</c>.
        /// </remarks>
        /// <param name="instance"><c>this</c> argument.</param>
        /// <param name="fieldName">The name of the instance field.</param>
        /// <returns>Field access expression.</returns>
        public static MemberExpression Field(this Expression instance, string fieldName)
            => Expression.Field(instance, fieldName);

        /// <summary>
        /// Constructs array element access expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.b[i]</c>.
        /// </remarks>
        /// <param name="array">The array expression.</param>
        /// <param name="indexes">Array element indicies.</param>
        /// <returns>Array element access expression.</returns>
        public static IndexExpression ElementAt(this Expression array, params Expression[] indexes)
            => Expression.ArrayAccess(array, indexes);

        /// <summary>
        /// Constructs collection or array element access expression.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="index">The index of the collection or array element.</param>
        /// <returns>The collection access expression.</returns>
        /// <exception cref="ArgumentException"><paramref name="collection"/> doesn't provide implicit support of Index expression.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/ranges">Ranges and Indicies</seealso>
        public static CollectionAccessExpression ElementAt(this Expression collection, ItemIndexExpression index)
            => new(collection, index);

        /// <summary>
        /// Constructs slice of collection or array.
        /// </summary>
        /// <param name="collection">The collection or array.</param>
        /// <param name="start">The first index of slice, inclusive.</param>
        /// <param name="end">The last index of slice, exclusive.</param>
        /// <returns>The slice of collection or array.</returns>
        public static SliceExpression Slice(this Expression collection, ItemIndexExpression? start = null, ItemIndexExpression? end = null)
            => collection.Slice(new RangeExpression(start, end));

        /// <summary>
        /// Constructs slice of collection or array.
        /// </summary>
        /// <param name="collection">The collection or array.</param>
        /// <param name="range">The range of collection or array.</param>
        /// <returns>The slice of collection or array.</returns>
        public static SliceExpression Slice(this Expression collection, Expression range)
            => new(collection, range);

        /// <summary>
        /// Constructs array length expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a.LongLength</c>.
        /// </remarks>
        /// <param name="array">The array expression.</param>
        /// <returns>Array length expression.</returns>
        public static UnaryExpression ArrayLength(this Expression array)
            => Expression.ArrayLength(array);

        /// <summary>
        /// Constructs expression representing count of items in the collection or string.
        /// </summary>
        /// <remarks>
        /// The input expression must be of type <see cref="string"/>, <see cref="StringBuilder"/>, array or any type
        /// implementing <see cref="ICollection{T}"/> or <see cref="IReadOnlyCollection{T}"/>.
        /// </remarks>
        /// <param name="collection">The expression representing collection.</param>
        /// <returns>The expression providing access to the appropriate property indicating the number of items in the collection.</returns>
        public static MemberExpression Count(this Expression collection)
        {
            if (collection.Type == typeof(string) || collection.Type == typeof(StringBuilder))
                return Expression.Property(collection, nameof(string.Length));
            var interfaceType = collection.Type.GetImplementedCollection() ?? throw new ArgumentException(ExceptionMessages.CollectionImplementationExpected);
            return Expression.Property(collection, interfaceType, nameof(Count));
        }

        /// <summary>
        /// Constructs expression that calls <see cref="object.ToString"/>.
        /// </summary>
        /// <param name="obj">The object to be converted into string.</param>
        /// <returns>The expression representing <c>ToString()</c> method call.</returns>
        public static MethodCallExpression AsString(this Expression obj) => Call(obj, nameof(ToString));

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
        /// The equivalent code is <c>goto label</c>.
        /// </remarks>
        /// <param name="label">The declared label.</param>
        /// <returns>Unconditional control transfer statement.</returns>
        public static GotoExpression Goto(this LabelTarget label) => Expression.Goto(label);

        /// <summary>
        /// Constructs unconditional control transfer expression.
        /// </summary>
        /// <param name="label">The declared label.</param>
        /// <param name="value">The value associated with the label.</param>
        /// <returns>Unconditional control transfer expression.</returns>
        public static GotoExpression Goto(this LabelTarget label, Expression value) => Expression.Goto(label, value);

        /// <summary>
        /// Constructs <c>return</c> statement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>return</c>.
        /// </remarks>
        /// <param name="label">The label representing function exit.</param>
        /// <returns>Return statement.</returns>
        public static GotoExpression Return(this LabelTarget label) => Expression.Return(label);

        /// <summary>
        /// Constructs <c>return</c> statement with given value.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>return a</c>.
        /// </remarks>
        /// <param name="label">The label representing function exit.</param>
        /// <param name="value">The value to be returned from function.</param>
        /// <returns>Return statement.</returns>
        public static GotoExpression Return(this LabelTarget label, Expression value) => Expression.Return(label, value);

        /// <summary>
        /// Constructs loop leave statement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>break</c>.
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
        /// The equivalent code is <c>label:</c>.
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
        /// The equivalent code is <c>a ? b : c</c>.
        /// </remarks>
        /// <param name="test">Test expression.</param>
        /// <param name="ifTrue">Positive branch.</param>
        /// <param name="ifFalse">Negative branch.</param>
        /// <param name="type">The type of conditional expression. Default is <see cref="void"/>.</param>
        /// <returns>Conditional expression.</returns>
        public static ConditionalExpression Condition(this Expression test, Expression? ifTrue = null, Expression? ifFalse = null, Type? type = null)
            => Expression.Condition(test, ifTrue ?? Expression.Empty(), ifFalse ?? Expression.Empty(), type ?? typeof(void));

        /// <summary>
        /// Constructs conditional expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a ? b : c</c>.
        /// </remarks>
        /// <typeparam name="TResult">The type of conditional expression. Default is <see cref="void"/>.</typeparam>
        /// <param name="test">Test expression.</param>
        /// <param name="ifTrue">Positive branch.</param>
        /// <param name="ifFalse">Negative branch.</param>
        /// <returns>Conditional expression.</returns>
        public static ConditionalExpression Condition<TResult>(this Expression test, Expression ifTrue, Expression ifFalse)
            => test.Condition(ifTrue, ifFalse, typeof(TResult));

        /// <summary>
        /// Constructs a <c>try</c> block with a <c>finally</c> block without <c>catch</c> block.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>try { } finally { }</c>.
        /// </remarks>
        /// <param name="try"><c>try</c> block.</param>
        /// <param name="finally"><c>finally</c> block.</param>
        /// <returns>Try-finally statement.</returns>
        public static TryExpression Finally(this Expression @try, Expression @finally) => Expression.TryFinally(@try, @finally);

        /// <summary>
        /// Constructs <c>throw</c> expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>throw e</c>.
        /// </remarks>
        /// <param name="exception">An exception to be thrown.</param>
        /// <param name="type">The type of expression. Default is <see cref="void"/>.</param>
        /// <returns><c>throw</c> expression.</returns>
        public static UnaryExpression Throw(this Expression exception, Type? type = null) => Expression.Throw(exception, type ?? typeof(void));

        /// <summary>
        /// Converts arbitrary value into constant expression.
        /// </summary>
        /// <typeparam name="T">The type of constant.</typeparam>
        /// <param name="value">The constant value.</param>
        /// <returns>The expression representing constant.</returns>
        public static ConstantExpression Const<T>(this T value) => Expression.Constant(value, typeof(T));

        /// <summary>
        /// Constructs type default value supplier.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>default(T)</c>.
        /// </remarks>
        /// <param name="type">The target type.</param>
        /// <returns>The type default value expression.</returns>
        public static DefaultExpression Default(this Type type) => Expression.Default(type);

        /// <summary>
        /// Constructs type instantiation expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>new T()</c>.
        /// </remarks>
        /// <param name="type">The type to be instantiated.</param>
        /// <param name="args">The list of arguments to be passed into constructor.</param>
        /// <returns>Instantiation expression.</returns>
        public static NewExpression New(this Type type, params Expression[] args)
        {
            if (args.LongLength == 0L)
                return Expression.New(type);
            ConstructorInfo? ctor = type.GetConstructor(Array.ConvertAll(args, static arg => arg.Type));
            return ctor is null ?
                throw new MissingMethodException(type.FullName, ConstructorInfo.ConstructorName) :
                Expression.New(ctor, args);
        }

        internal static Expression AddPrologue(this Expression expression, bool inferType, IReadOnlyCollection<Expression> instructions)
        {
            if (instructions.Count == 0)
                return expression;
            if (expression is BlockExpression block)
                return Expression.Block(inferType ? block.Type : typeof(void), block.Variables, instructions.Concat(block.Expressions));
            return Expression.Block(inferType ? expression.Type : typeof(void), instructions.Append(expression));
        }

        internal static Expression AddEpilogue(this Expression expression, bool inferType, IReadOnlyCollection<Expression> instructions)
        {
            if (instructions.Count == 0)
                return expression;

            IEnumerable<Expression> result;
            IEnumerable<ParameterExpression> variables;
            if (expression is BlockExpression block)
            {
                variables = block.Variables;
                result = block.Expressions.Concat(instructions);
            }
            else
            {
                variables = Empty<ParameterExpression>();
                result = instructions.Prepend(expression);
            }

            return Expression.Block(inferType ? result.Last().Type : typeof(void), variables, result);
        }

        internal static Expression AddPrologue(this Expression expression, bool inferType, params Expression[] instructions)
            => AddPrologue(expression, inferType, (IReadOnlyCollection<Expression>)instructions);

        internal static Expression AddEpilogue(this Expression expression, bool inferType, params Expression[] instructions)
            => AddEpilogue(expression, inferType, (IReadOnlyCollection<Expression>)instructions);

        /// <summary>
        /// Constructs type instantiation expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>new T()</c>.
        /// </remarks>
        /// <param name="type">The expression representing the type to be instantiated.</param>
        /// <param name="args">The list of arguments to be passed into constructor.</param>
        /// <returns>Instantiation expression.</returns>
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Activator))]
        public static MethodCallExpression New(this Expression type, params Expression[] args)
        {
            var activate = typeof(Activator).GetMethod(nameof(Activator.CreateInstance), new[] { typeof(Type), typeof(object[]) });
            Debug.Assert(activate is not null);
            return Expression.Call(activate, type, Expression.NewArrayInit(typeof(object), args));
        }

        /// <summary>
        /// Creates <c>foreach</c> loop expression.
        /// </summary>
        /// <param name="collection">The collection to iterate through.</param>
        /// <param name="body">A delegate that is used to construct the body of the loop.</param>
        /// <returns>The constructed loop.</returns>
        public static ForEachExpression ForEach(this Expression collection, ForEachExpression.Statement body)
            => ForEachExpression.Create(collection, body);

        /// <summary>
        /// Creates <c>for</c> loop expression.
        /// </summary>
        /// <param name="initialization">Loop variable initialization expression.</param>
        /// <param name="condition">The condition of loop continuation.</param>
        /// <param name="iteration">The loop iteration statement.</param>
        /// <param name="body">The loop body.</param>
        /// <returns>The constructed loop.</returns>
        public static ForExpression For(this Expression initialization, ForExpression.LoopBuilder.Condition condition, ForExpression.LoopBuilder.Iteration iteration, ForExpression.LoopBuilder.Statement body)
            => ForExpression.Builder(initialization).While(condition).Do(body).Iterate(iteration).Build();

        /// <summary>
        /// Creates a new synchronized block of code.
        /// </summary>
        /// <param name="syncRoot">The monitor object.</param>
        /// <param name="body">The delegate used to construct synchronized block of code.</param>
        /// <returns>The synchronized block of code.</returns>
        public static LockExpression Lock(this Expression syncRoot, LockExpression.Statement body)
            => LockExpression.Create(syncRoot, body);

        /// <summary>
        /// Creates block of code associated with disposable resource.
        /// </summary>
        /// <param name="resource">The disposable resource.</param>
        /// <param name="body">The delegate used to construct the block of code.</param>
        /// <returns>The constructed expression.</returns>
        public static UsingExpression Using(this Expression resource, UsingExpression.Statement body)
            => UsingExpression.Create(resource, body);

        /// <summary>
        /// Creates <c>while</c> loop expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>while(condition) {  }</c>.
        /// </remarks>
        /// <param name="condition">The loop condition.</param>
        /// <param name="body">The delegate that is used to construct loop body.</param>
        /// <returns>The constructed loop expression.</returns>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/while">while Statement</seealso>
        public static WhileExpression While(this Expression condition, WhileExpression.Statement body)
            => WhileExpression.Create(condition, body, true);

        /// <summary>
        /// Creates <c>do{ }while()</c> loop expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>do { } while(condition)</c>.
        /// </remarks>
        /// <param name="condition">The loop condition.</param>
        /// <param name="body">The delegate that is used to construct loop body.</param>
        /// <returns>The constructed loop expression.</returns>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/do">do-while Statement</seealso>
        public static WhileExpression Until(this Expression condition, WhileExpression.Statement body)
            => WhileExpression.Create(condition, body, false);

        /// <summary>
        /// Creates a new instance of <see cref="WithExpression"/>.
        /// </summary>
        /// <param name="obj">The object to be referred inside of the body.</param>
        /// <param name="body">The body of the expression.</param>
        /// <returns>The constructed expression.</returns>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/with-end-with-statement">With..End Statement</seealso>
        public static WithExpression With(this Expression obj, WithExpression.Statement body)
            => WithExpression.Create(obj, body);

        internal static MethodCallExpression Breakpoint() => CallStatic(typeof(Debugger), nameof(Debugger.Break));

        internal static MethodCallExpression Assert(this Expression test, string? message)
        {
            if (test is null)
                throw new ArgumentNullException(nameof(test));
            else if (test.Type != typeof(bool))
                throw new ArgumentException(ExceptionMessages.TypeExpected<bool>(), nameof(test));
            else if (string.IsNullOrEmpty(message))
                return CallStatic(typeof(Debug), nameof(Debug.Assert), test);
            else
                return CallStatic(typeof(Debug), nameof(Debug.Assert), test, Const(message));
        }

        /// <summary>
        /// Creates a new safe navigation expression.
        /// </summary>
        /// <param name="target">The expression that is guarded by <see langword="null"/> check.</param>
        /// <param name="body">The body to be executed if <paramref name="target"/> is not <see langword="null"/>. </param>
        /// <returns>The expression representing safe navigation.</returns>
        public static NullSafetyExpression IfNotNull(this Expression target, Func<ParameterExpression, Expression> body)
            => NullSafetyExpression.Create(target, body);

        /// <summary>
        /// Creates a new expression that is equal to <c>refanyval</c> IL instruction.
        /// </summary>
        /// <param name="typedRef">The variable of type <see cref="TypedReference"/>.</param>
        /// <param name="referenceType">The type of the managed reference.</param>
        /// <returns>The expression representing statically typed referenced.</returns>
        public static RefAnyValExpression RefAnyVal(this ParameterExpression typedRef, Type referenceType)
            => new(typedRef, referenceType);

        /// <summary>
        /// Creates a new expression that is equal to <c>refanyval</c> IL instruction.
        /// </summary>
        /// <param name="typedRef">The variable of type <see cref="TypedReference"/>.</param>
        /// <typeparam name="T">The type of the managed reference.</typeparam>
        /// <returns>The expression representing statically typed referenced.</returns>
        public static RefAnyValExpression RefAnyVal<T>(this ParameterExpression typedRef)
            => RefAnyVal(typedRef, typeof(T));

        /// <summary>
        /// Constructs expression of type <see cref="System.Index"/>.
        /// </summary>
        /// <param name="value">The expression representing index value.</param>
        /// <param name="fromEnd">A boolean indicating if the index is from the start (<see langword="false"/>) or from the end (<see langword="true"/>) of a collection.</param>
        /// <returns>Index expression.</returns>
        /// <exception cref="ArgumentException">Type of <paramref name="value"/> should be <see cref="int"/>, <see cref="short"/>, <see cref="byte"/> or <see cref="sbyte"/>.</exception>
        public static ItemIndexExpression Index(this Expression value, bool fromEnd)
            => new(value, fromEnd);

        /// <summary>
        /// Constructs expression of type <see cref="System.Index"/>.
        /// </summary>
        /// <param name="value">The expression representing index value.</param>
        /// <param name="fromEnd">A boolean indicating if the index is from the start (<see langword="false"/>) or from the end (<see langword="true"/>) of a collection.</param>
        /// <returns>Index expression.</returns>
        public static ItemIndexExpression Index(this int value, bool fromEnd)
            => Index(Const(value), fromEnd);

        /// <summary>
        /// Converts index to equivalent expression.
        /// </summary>
        /// <param name="index">The index value.</param>
        /// <returns>Index expression.</returns>
        public static ItemIndexExpression Quote(this in Index index)
            => Index(index.Value, index.IsFromEnd);

        /// <summary>
        /// Constructs range.
        /// </summary>
        /// <param name="start">The inclusive start index of the range.</param>
        /// <param name="end">The exclusive end index of the range.</param>
        /// <returns>The range expression.</returns>
        public static RangeExpression To(this ItemIndexExpression start, ItemIndexExpression end)
            => new(start, end);

        /// <summary>
        /// Constructs range.
        /// </summary>
        /// <param name="start">The inclusive start index of the range.</param>
        /// <param name="end">The exclusive end index of the range.</param>
        /// <returns>The range expression.</returns>
        public static RangeExpression To(this ItemIndexExpression start, Index end)
            => start.To(end.Quote());

        /// <summary>
        /// Converts range to equivalent expression.
        /// </summary>
        /// <param name="range">The range to convert.</param>
        /// <returns>The expression representing given range.</returns>
        public static RangeExpression Quote(this in Range range)
            => range.Start.Quote().To(range.End.Quote());

        /// <summary>
        /// Constructs null-coalescing assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>left ??= right;</c>.
        /// </remarks>
        /// <param name="left">The left operand of the assignment.</param>
        /// <param name="right">The right operand of the assignment.</param>
        /// <returns>The constructed expression.</returns>
        /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="left"/>.</exception>
        public static NullCoalescingAssignmentExpression NullCoalescingAssignment(this ParameterExpression left, Expression right)
            => new(left, right);

        /// <summary>
        /// Constructs null-coalescing assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>left.Member ??= right;</c>.
        /// </remarks>
        /// <param name="left">The left operand of the assignment.</param>
        /// <param name="right">The right operand of the assignment.</param>
        /// <returns>The constructed expression.</returns>
        /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="left"/>.</exception>
        public static NullCoalescingAssignmentExpression NullCoalescingAssignment(this MemberExpression left, Expression right)
            => new(left, right);

        /// <summary>
        /// Constructs null-coalescing assignment expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>left.Member ??= right;</c>.
        /// </remarks>
        /// <param name="left">The left operand of the assignment.</param>
        /// <param name="right">The right operand of the assignment.</param>
        /// <returns>The constructed expression.</returns>
        /// <exception cref="ArgumentException"><paramref name="right"/> is not assignable to <paramref name="left"/>.</exception>
        public static NullCoalescingAssignmentExpression NullCoalescingAssignment(this IndexExpression left, Expression right)
            => new(left, right);

        /// <summary>
        /// Converts value type to the expression of <see cref="Nullable{T}"/> type.
        /// </summary>
        /// <remarks>
        /// If <paramref name="expression"/> is of pointer of reference type then
        /// method returns unmodified expression.
        /// </remarks>
        /// <param name="expression">The expression to be converted.</param>
        /// <returns>The nullable expression.</returns>
        public static Expression AsNullable(this Expression expression)
            => Nullable.GetUnderlyingType(expression.Type) is null && !expression.Type.IsPointer && expression.Type.IsValueType ? Expression.Convert(expression, typeof(Nullable<>).MakeGenericType(expression.Type)) : expression;

        /// <summary>
        /// Creates the expression of <see cref="Optional{T}"/> type.
        /// </summary>
        /// <param name="expression">The expression to be converted.</param>
        /// <returns>The expression of <see cref="Optional{T}"/> type.</returns>
        public static Expression AsOptional(this Expression expression)
            => Expression.Convert(expression, typeof(Optional<>).MakeGenericType(expression.Type));

        /// <summary>
        /// Converts compound expression to its safe equivalent
        /// that doesn't throw exception and return <see cref="Result{T}"/> instead.
        /// </summary>
        /// <param name="expression">The compound expression.</param>
        /// <returns>The expression of type <see cref="Result{T}"/>.</returns>
        public static Expression AsResult(this Expression expression)
        {
            var exception = Expression.Parameter(typeof(Exception));
            var ctor = typeof(Result<>).MakeGenericType(expression.Type).GetConstructor(new[] { expression.Type });
            Debug.Assert(ctor?.DeclaringType is not null);
            var fallbackCtor = ctor.DeclaringType.GetConstructor(new[] { typeof(Exception) });
            Debug.Assert(fallbackCtor is not null);
            return Expression.TryCatch(
                Expression.New(ctor, expression),
                Expression.Catch(exception, Expression.New(fallbackCtor, exception)));
        }

        internal static IndexExpression MakeIndex(Expression target, Expression[] args)
        {
            // handle case for array
            if (target.Type.IsArray)
                return Expression.MakeIndex(target, null, args);

            // not an array, looking for DefaultMemberAttribute
            var attribute = target.Type.GetCustomAttribute<DefaultMemberAttribute>(true);
            if (attribute is null)
                throw new NotSupportedException();

            return Expression.Property(target, attribute.MemberName, args);
        }
    }
}