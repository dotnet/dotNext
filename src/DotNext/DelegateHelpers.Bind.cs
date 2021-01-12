using System;

namespace DotNext
{
    public static partial class DelegateHelpers
    {
        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action Bind<T>(this Action<T> action, T obj)
            where T : class
        {
            // TODO: Should be generalized using function pointer in C# 9
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T>.Create(action, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<TResult> Bind<T, TResult>(this Func<T, TResult> func, T obj)
            where T : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<T2, TResult> Bind<T1, T2, TResult>(this Func<T1, T2, TResult> func, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<T2, TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T1>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action<T2> Bind<T1, T2>(this Action<T1, T2> action, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action<T2>, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T1>.Create(action, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<T2, T3, TResult> Bind<T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> func, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<T2, T3, TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T1>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action<T2, T3> Bind<T1, T2, T3>(this Action<T1, T2, T3> action, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action<T2, T3>, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T1>.Create(action, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<T2, T3, T4, TResult> Bind<T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, TResult> func, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<T2, T3, T4, TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T1>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action<T2, T3, T4> Bind<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action<T2, T3, T4>, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T1>.Create(action, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Func<T2, T3, T4, T5, TResult> Bind<T1, T2, T3, T4, T5, TResult>(this Func<T1, T2, T3, T4, T5, TResult> func, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (func.Target is null)
                return ChangeType<Func<T2, T3, T4, T5, TResult>, TargetRewriter>(func, new TargetRewriter(obj));
            return Closure<T1>.Create(func, obj);
        }

        /// <summary>
        /// Produces delegate which first parameter is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public static Action<T2, T3, T4, T5> Bind<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 obj)
            where T1 : class
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            if (action.Target is null)
                return ChangeType<Action<T2, T3, T4, T5>, TargetRewriter>(action, new TargetRewriter(obj));
            return Closure<T1>.Create(action, obj);
        }
    }
}