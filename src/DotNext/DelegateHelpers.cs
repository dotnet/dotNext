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
            MemberExpression { Member: PropertyInfo { CanRead: true } property } => property.GetMethod!,
            BinaryExpression { Method: not null } expr => expr.Method,
            IndexExpression { Indexer: { CanRead: true } } expr => expr.Indexer.GetMethod!,
            UnaryExpression { Method: not null } expr => expr.Method,
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
    public static TDelegate CreateOpenDelegate<TDelegate>(Expression<TDelegate> expression)
        where TDelegate : Delegate
        => GetMethod(expression).CreateDelegate<TDelegate>();

    /// <summary>
    /// Creates open delegate for instance property setter.
    /// </summary>
    /// <param name="properyExpr">The expression representing property.</param>
    /// <typeparam name="T">The declaring type.</typeparam>
    /// <typeparam name="TValue">The type of property value.</typeparam>
    /// <returns>The open delegate representing property setter.</returns>
    public static Action<T, TValue> CreateOpenDelegate<T, TValue>(Expression<Func<T, TValue>> properyExpr)
        where T : class
        => properyExpr.Body is MemberExpression { Member: PropertyInfo { CanWrite: true } property } ?
            property.SetMethod!.CreateDelegate<Action<T, TValue>>() :
            throw new ArgumentException(ExceptionMessages.InvalidExpressionTree, nameof(properyExpr));

    /// <summary>
    /// Creates a factory for closed delegates.
    /// </summary>
    /// <param name="expression">The expression tree containing instance method, property, operator call.</param>
    /// <typeparam name="TDelegate">The type of the delegate describing expression tree.</typeparam>
    /// <returns>The factory of closed delegate.</returns>
    public static Func<object, TDelegate> CreateClosedDelegateFactory<TDelegate>(Expression<TDelegate> expression)
        where TDelegate : Delegate
        => new(GetMethod(expression).CreateDelegate<TDelegate>);

    /// <summary>
    /// Performs contravariant conversion
    /// of actual generic argument specified
    /// for <see cref="EventHandler{TEventArgs}"/> type.
    /// </summary>
    /// <typeparam name="TBase">Input type of the delegate.</typeparam>
    /// <typeparam name="T">A subtype of <typeparamref name="TBase"/>.</typeparam>
    /// <param name="handler">The handler to convert.</param>
    /// <returns>The delegate referencing the same method as original delegate.</returns>
    /// <remarks>
    /// Generic parameter of delegate <see cref="EventHandler{TEventArgs}"/>
    /// is not marked as <see langword="in"/> so compiler doesn't
    /// support contravariant conversion for it. This method
    /// provides contravariant conversion for this delegate type.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventHandler<T> Contravariant<TBase, T>(this EventHandler<TBase> handler)
        where TBase : class
        where T : class, TBase
        => handler.ChangeType<EventHandler<T>>();

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