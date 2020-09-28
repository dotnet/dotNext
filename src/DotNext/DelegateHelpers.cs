using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext
{
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents various extensions of delegates.
    /// </summary>
    [BeforeFieldInit(true)]
    public static partial class DelegateHelpers
    {
        private static readonly Predicate<Assembly>? IsCollectible;
        private static readonly SendOrPostCallback SendOrPostInvoker;
        private static readonly Action<Action> ActionInvoker;

        static DelegateHelpers()
        {
            // TODO: Should be replaced with direct call without reflection in .NET Core 5
            var isCollectibleGetter = typeof(Assembly).GetProperty("IsCollectible", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)?.GetMethod;
            IsCollectible = isCollectibleGetter?.CreateDelegate<Predicate<Assembly>>();
            SendOrPostInvoker = UnsafeInvoke;
            ActionInvoker = UnsafeInvoke;

            static void UnsafeInvoke(object action) => Unsafe.As<Action>(action).Invoke();
        }

        private static MethodInfo GetMethod<TDelegate>(Expression<TDelegate> expression)
            where TDelegate : Delegate
            => expression.Body switch
            {
                MethodCallExpression expr => expr.Method,
                MemberExpression expr when expr.Member is PropertyInfo property => property.GetMethod,
                BinaryExpression expr => expr.Method,
                IndexExpression expr => expr.Indexer.GetMethod,
                UnaryExpression expr => expr.Method,
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
            => properyExpr.Body is MemberExpression expr && expr.Member is PropertyInfo property && property.CanWrite ?
                property.SetMethod.CreateDelegate<Action<T, TValue>>() :
                throw new ArgumentException(ExceptionMessages.InvalidExpressionTree, nameof(properyExpr));

        /// <summary>
        /// Creates a factory for closed delegates.
        /// </summary>
        /// <param name="expression">The expression tree containing instance method, property, operator call.</param>
        /// <typeparam name="TDelegate">The type of the delegate describing expression tree.</typeparam>
        /// <returns>The factory of closed delegate.</returns>
        public static Func<object, TDelegate>? CreateClosedDelegateFactory<TDelegate>(Expression<TDelegate> expression)
            where TDelegate : Delegate
        {
            var method = GetMethod(expression);
            return method is null ? null : new Func<object, TDelegate>(method.CreateDelegate<TDelegate>);
        }

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
        /// Creates a delegate of the specified type with the specified target from this method.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate to create.</typeparam>
        /// <param name="method">The method to be wrapped into delegate.</param>
        /// <param name="target">The object targeted by the delegate.</param>
        /// <returns>The delegate for the specified method.</returns>
        /// <seealso cref="MethodInfo.CreateDelegate(Type, object)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TDelegate CreateDelegate<TDelegate>(this MethodInfo method, object? target = null)
            where TDelegate : Delegate
            => (TDelegate)method.CreateDelegate(typeof(TDelegate), target);

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

        internal static void InvokeInContext(this Action action, SynchronizationContext context)
            => context.Post(SendOrPostInvoker, action);

        internal static void InvokeInThreadPool(this Action action)
            => ThreadPool.QueueUserWorkItem(ActionInvoker, action, false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanBeUnloaded(Assembly assembly)
            => assembly.IsDynamic || assembly.ReflectionOnly || (IsCollectible?.Invoke(assembly) ?? false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsRegularDelegate(Delegate d)
            => (d.Method.Attributes & MethodAttributes.Static) == 0 || d.Target != null || CanBeUnloaded(d.Method.Module.Assembly);
    }
}
