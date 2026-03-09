using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext;

/// <summary>
/// Represents various extensions of delegates.
/// </summary>
public static partial class DelegateHelpers
{
    private static MethodInfo GetMethod<TDelegate>(Expression<TDelegate> expression)
        where TDelegate : Delegate
        => expression.Body switch
        {
            MethodCallExpression expr => expr.Method,
            MemberExpression { Member: PropertyInfo { GetMethod: { } getter } } => getter,
            BinaryExpression { Method: { } method } => method,
            IndexExpression { Indexer.GetMethod: { } getter } => getter,
            UnaryExpression { Method: { } method } => method,
            _ => throw new ArgumentException(ExceptionMessages.InvalidExpressionTree, nameof(expression))
        };

    /// <summary>
    /// Creates open delegate for the instance method, property, operator referenced
    /// in expression tree.
    /// </summary>
    /// <typeparam name="TDelegate">The type of the delegate describing expression tree.</typeparam>
    /// <param name="expression">The expression tree containing instance method call.</param>
    /// <returns>The open delegate.</returns>
    /// <exception cref="ArgumentException"><paramref name="expression"/> is not valid expression tree.</exception>
    public static TDelegate CreateOpenDelegate<TDelegate>(this Expression<TDelegate> expression)
        where TDelegate : Delegate
        => GetMethod(expression).CreateDelegate<TDelegate>();

    /// <summary>
    /// Creates open delegate for instance property setter.
    /// </summary>
    /// <param name="propertyExpr">The expression representing property.</param>
    /// <typeparam name="T">The declaring type.</typeparam>
    /// <typeparam name="TValue">The type of property value.</typeparam>
    /// <returns>The open delegate representing property setter.</returns>
    public static Action<T, TValue> CreateOpenDelegate<T, TValue>(this Expression<Func<T, TValue>> propertyExpr)
        where T : class
        where TValue : allows ref struct
        => propertyExpr.Body is MemberExpression { Member: PropertyInfo { SetMethod: { } setter } }
            ? setter.CreateDelegate<Action<T, TValue>>()
            : throw new ArgumentException(ExceptionMessages.InvalidExpressionTree, nameof(propertyExpr));

    /// <summary>
    /// Creates a factory for closed delegates.
    /// </summary>
    /// <param name="expression">The expression tree containing instance method, property, operator call.</param>
    /// <typeparam name="TDelegate">The type of the delegate describing expression tree.</typeparam>
    /// <returns>The factory of closed delegate.</returns>
    public static Func<object, TDelegate> CreateClosedDelegateFactory<TDelegate>(this Expression<TDelegate> expression)
        where TDelegate : Delegate
        => GetMethod(expression).CreateDelegate<TDelegate>;

    /// <summary>
    /// Returns a new delegate of different type which
    /// points to the same method as original delegate.
    /// </summary>
    /// <param name="d">Delegate to convert.</param>
    /// <typeparam name="TDelegate">A new delegate type.</typeparam>
    /// <returns>A method wrapped into new delegate type.</returns>
    /// <exception cref="ArgumentException">Cannot convert delegate type.</exception>
    public static TDelegate ChangeType<TDelegate>(this Delegate d)
        where TDelegate : Delegate
        => d is TDelegate ? Unsafe.As<TDelegate>(d) : ChangeType<TDelegate, EmptyTargetRewriter>(d, new EmptyTargetRewriter());
}