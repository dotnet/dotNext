using System;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents a source of static method pointers.
    /// </summary>
    /// <remarks>
    /// The reason to having this method is to avoid heap allocations every time
    /// when you need typed method pointer. The constructor of such pointer
    /// performs runtime checks using Reflection. These checks require such allocations.
    /// To avoid that, it is possible to create <see cref="MethodCookie{D}"/>
    /// once and store it in <c>static readonly</c> field. After that, every creation
    /// of typed method pointer doesn't produce unecessary memory allocations.
    /// </remarks>
    /// <typeparam name="D">The type of the delegate compatible with managed pointer.</typeparam>
    public readonly struct MethodCookie<D>
        where D : Delegate
    {
        internal readonly RuntimeMethodHandle MethodHandle;

        /// <summary>
        /// Initializes a new static method cookie.
        /// </summary>
        /// <param name="delegate">The delegate referencing a static method for which pointers should be created.</param>
        public MethodCookie(D @delegate)
        {
            MethodHandle = @delegate.Target is null ? @delegate.Method.MethodHandle : throw new ArgumentException(ExceptionMessages.InvalidMethodSignature, nameof(@delegate));
        }

        /// <summary>
        /// Initializes a new static method cookie.
        /// </summary>
        /// <param name="method">A static method for which pointers should be created.</param>
        public MethodCookie(MethodInfo method)
            : this(method.CreateDelegate<D>())
        {
        }

        /// <summary>
        /// Gets method referenced by this cookie.
        /// </summary>
        public MethodBase Method => MethodBase.GetMethodFromHandle(MethodHandle);
    }

    /// <summary>
    /// Represents a source of instance method pointers.
    /// </summary>
    /// <remarks>
    /// The reason to having this method is to avoid heap allocations every time
    /// when you need typed method pointer. The constructor of such pointer
    /// performs runtime checks using Reflection. These checks require such allocations.
    /// To avoid that, it is possible to create <see cref="MethodCookie{D}"/>
    /// once and store it in <c>static readonly</c> field. After that, every creation
    /// of typed method pointer doesn't produce unecessary memory allocations.
    /// </remarks>
    /// <typeparam name="T">The type of the method target.</typeparam>
    /// <typeparam name="D">The type of the delegate compatible with managed pointer.</typeparam>
    public readonly struct MethodCookie<T, D>
        where D : Delegate
        where T : class
    {
        internal readonly RuntimeMethodHandle MethodHandle;

        /// <summary>
        /// Initializes a new instance method cookie.
        /// </summary>
        /// <param name="delegate">The delegate referencing an instance method for which pointers should be created.</param>
        public MethodCookie(D @delegate)
        {
            MethodHandle = @delegate.Target is T ? @delegate.Method.MethodHandle : throw new ArgumentException(ExceptionMessages.InvalidMethodSignature, nameof(@delegate)); 
        }

        /// <summary>
        /// Initializes a new instance method cookie.
        /// </summary>
        /// <param name="method">An instance method for which pointers should be created.</param>
        public MethodCookie(MethodInfo method)
        {
            Type[] expectedParams;
            Type expectedReturnType;
            {
                var invokeMethod = DelegateType.GetInvokeMethod<D>();
                expectedParams = invokeMethod.GetParameterTypes();
                expectedReturnType = invokeMethod.ReturnType;
            }
            bool thisTypeOk;
            if(method.IsStatic)
            {
                expectedParams = expectedParams.Insert(typeof(T), 0L);
                thisTypeOk = true;
            }
            else
                thisTypeOk = method.DeclaringType.IsAssignableFrom(typeof(T));
            if(thisTypeOk && method.SignatureEquals(expectedParams) && expectedReturnType.IsAssignableFrom(method.ReturnType))
                MethodHandle = method.MethodHandle;
            else
                throw new ArgumentException(ExceptionMessages.InvalidMethodSignature, nameof(method));
        }

        /// <summary>
        /// Gets method referenced by this cookie.
        /// </summary>
        public MethodBase Method => MethodBase.GetMethodFromHandle(MethodHandle);
    }

    /// <summary>
    /// Provides factory methods for creating typed method pointers using method cookie.
    /// </summary>
    public static class MethodCookie
    {
        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static ActionPointer CreatePointer(this in MethodCookie<Action> cookie) => new ActionPointer(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="T">The type of the object to be targeted by method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static ActionPointer CreatePointer<T>(this in MethodCookie<T, Action> cookie, T obj) where T : class => new ActionPointer(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static FunctionPointer<R> CreatePointer<R>(this MethodCookie<Func<R>> cookie) => new FunctionPointer<R>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="T">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static FunctionPointer<R> CreatePointer<T, R>(this MethodCookie<T, Func<R>> cookie, T obj) where T : class => new FunctionPointer<R>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static predicate.
        /// </summary>
        /// <typeparam name="T">The type of the first argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static predicate.</returns>
        public static PredicatePointer<T> CreatePointer<T>(this in MethodCookie<Predicate<T>> cookie) => new PredicatePointer<T>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance predicate.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T">The type of the first argument to be passed into method pointer.</typeparam>       
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance predicate.</returns>
        public static PredicatePointer<T> CreatePointer<S, T>(this in MethodCookie<Predicate<T>> cookie, S obj) where S : class => new PredicatePointer<T>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T">The type of the first argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static ActionPointer<T> CreatePointer<T>(this in MethodCookie<Action<T>> cookie) => new ActionPointer<T>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T">The type of the first argument to be passed into method pointer.</typeparam>       
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static ActionPointer<T> CreatePointer<S, T>(this in MethodCookie<S, Action<T>> cookie, S obj) where S : class => new ActionPointer<T>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static FunctionPointer<T, R> CreatePointer<T, R>(this in MethodCookie<Func<T, R>> cookie) => new FunctionPointer<T, R>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static FunctionPointer<T, R> CreatePointer<S, T, R>(this in MethodCookie<S, Func<T, R>> cookie, S obj) where S : class => new FunctionPointer<T, R>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static ActionPointer<T1, T2> CreatePointer<T1, T2>(this in MethodCookie<Action<T1, T2>> cookie) => new ActionPointer<T1, T2>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>    
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static ActionPointer<T1, T2> CreatePointer<S, T1, T2>(this in MethodCookie<S, Action<T1, T2>> cookie, S obj) where S : class => new ActionPointer<T1, T2>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>    
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static FunctionPointer<T1, T2, R> CreatePointer<T1, T2, R>(this in MethodCookie<Func<T1, T2, R>> cookie) => new FunctionPointer<T1, T2, R>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static FunctionPointer<T1, T2, R> CreatePointer<S, T1, T2, R>(this in MethodCookie<S, Func<T1, T2, R>> cookie, S obj) where S : class => new FunctionPointer<T1, T2, R>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static ActionPointer<T1, T2, T3> CreatePointer<T1, T2, T3>(this in MethodCookie<Action<T1, T2, T3>> cookie) => new ActionPointer<T1, T2, T3>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>    
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static ActionPointer<T1, T2, T3> CreatePointer<S, T1, T2, T3>(this in MethodCookie<S, Action<T1, T2, T3>> cookie, S obj) where S : class => new ActionPointer<T1, T2, T3>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>    
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static FunctionPointer<T1, T2, T3, R> CreatePointer<T1, T2, T3, R>(this in MethodCookie<Func<T1, T2, T3, R>> cookie) => new FunctionPointer<T1, T2, T3, R>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static FunctionPointer<T1, T2, T3, R> CreatePointer<S, T1, T2, T3, R>(this in MethodCookie<S, Func<T1, T2, T3, R>> cookie, S obj) where S : class => new FunctionPointer<T1, T2, T3, R>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static ActionPointer<T1, T2, T3, T4> CreatePointer<T1, T2, T3, T4>(this in MethodCookie<Action<T1, T2, T3, T4>> cookie) => new ActionPointer<T1, T2, T3, T4>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>    
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static ActionPointer<T1, T2, T3, T4> CreatePointer<S, T1, T2, T3, T4>(this in MethodCookie<S, Action<T1, T2, T3, T4>> cookie, S obj) where S : class => new ActionPointer<T1, T2, T3, T4>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>    
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static FunctionPointer<T1, T2, T3, T4, R> CreatePointer<T1, T2, T3, T4, R>(this in MethodCookie<Func<T1, T2, T3, T4, R>> cookie) => new FunctionPointer<T1, T2, T3, T4, R>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static FunctionPointer<T1, T2, T3, T4, R> CreatePointer<S, T1, T2, T3, T4, R>(this in MethodCookie<S, Func<T1, T2, T3, T4, R>> cookie, S obj) where S : class => new FunctionPointer<T1, T2, T3, T4, R>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T5">The type of the fifth argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static ActionPointer<T1, T2, T3, T4, T5> CreatePointer<T1, T2, T3, T4, T5>(this in MethodCookie<Action<T1, T2, T3, T4, T5>> cookie) => new ActionPointer<T1, T2, T3, T4, T5>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>    
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T5">The type of the fifth argument to be passed into method pointer.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static ActionPointer<T1, T2, T3, T4, T5> CreatePointer<S, T1, T2, T3, T4, T5>(this in MethodCookie<S, Action<T1, T2, T3, T4, T5>> cookie, S obj) where S : class => new ActionPointer<T1, T2, T3, T4, T5>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Creates pointer to the static method.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>    
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T5">The type of the fifth argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <returns>The pointer to the static method.</returns>
        public static FunctionPointer<T1, T2, T3, T4, T5, R> CreatePointer<T1, T2, T3, T4, T5, R>(this in MethodCookie<Func<T1, T2, T3, T4, T5, R>> cookie) => new FunctionPointer<T1, T2, T3, T4, T5, R>(cookie.MethodHandle, null);

        /// <summary>
        /// Creates pointer to the instance method method.
        /// </summary>
        /// <typeparam name="S">The type of the object to be targeted by method pointer.</typeparam>
        /// <typeparam name="T1">The type of the first argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T2">The type of the second argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T3">The type of the third argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T4">The type of the fourth argument to be passed into method pointer.</typeparam>
        /// <typeparam name="T5">The type of the fifth argument to be passed into method pointer.</typeparam>
        /// <typeparam name="R">The return type.</typeparam>
        /// <param name="cookie">The object representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The pointer to the instance method.</returns>
        public static FunctionPointer<T1, T2, T3, T4, T5, R> CreatePointer<S, T1, T2, T3, T4, T5, R>(this in MethodCookie<S, Func<T1, T2, T3, T4, T5, R>> cookie, S obj) where S : class => new FunctionPointer<T1, T2, T3, T4, T5, R>(cookie.MethodHandle, obj ?? throw new ArgumentNullException(nameof(obj)));
    }
}