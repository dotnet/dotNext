namespace DotNext;

public static partial class DelegateHelpers
{
    private static unsafe TOutput Bind<TInput, TOutput, T>(TInput d, T obj, delegate*<TInput, T, TOutput> closureFactory)
        where TInput : MulticastDelegate
        where TOutput : MulticastDelegate
        where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);

        return d.Target is null ?
            ChangeType<TOutput, TargetRewriter>(d, new TargetRewriter(obj)) :
            closureFactory(d, obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Action{T}"/>.
    /// </summary>
    /// <param name="receiver">The action to bind.</param>
    /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
    extension<T>(Action<T> receiver) where T : class
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Action Bind(T obj)
            => Bind(receiver, obj, &Closure<T>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Action operator <<(Action<T> action, T obj) => action.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Func{T, TResult}"/>.
    /// </summary>
    /// <param name="receiver">The function to bind.</param>
    /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    extension<T, TResult>(Func<T, TResult> receiver)
        where T : class
        where TResult : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Func<TResult> Bind(T obj)
            => Bind(receiver, obj, &Closure<T>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="func">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Func<TResult> operator <<(Func<T, TResult> func, T obj) => func.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Func{T1, T2, TResult}"/>.
    /// </summary>
    /// <param name="receiver">The function to bind.</param>
    /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    extension<T1, T2, TResult>(Func<T1, T2, TResult> receiver)
        where T1 : class
        where T2 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Func<T2, TResult> Bind(T1 obj)
            => Bind(receiver, obj, &Closure<T1>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="func">The function to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Func<T2, TResult> operator <<(Func<T1, T2, TResult> func, T1 obj) => func.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Action{T1, T2}"/>.
    /// </summary>
    /// <param name="receiver">The action to bind.</param>
    /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    extension<T1, T2>(Action<T1, T2> receiver)
        where T1 : class
        where T2 : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Action<T2> Bind(T1 obj)
            => Bind(receiver, obj, &Closure<T1>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Action<T2> operator <<(Action<T1, T2> action, T1 obj)
            => action.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Func{T1, T2, T3, TResult}"/>.
    /// </summary>
    /// <param name="receiver">The function to bind.</param>
    /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    extension<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> receiver)
        where T1 : class
        where T2 : allows ref struct
        where T3 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Func<T2, T3, TResult> Bind(T1 obj)
            => Bind(receiver, obj, &Closure<T1>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="func">The function to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Func<T2, T3, TResult> operator <<(Func<T1, T2, T3, TResult> func, T1 obj)
            => func.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Action{T1, T2, T3}"/>.
    /// </summary>
    /// <param name="receiver">The action to bind.</param>
    /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    extension<T1, T2, T3>(Action<T1, T2, T3> receiver)
        where T1 : class
        where T2 : allows ref struct
        where T3 : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Action<T2, T3> Bind(T1 obj)
            => Bind(receiver, obj, &Closure<T1>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Action<T2, T3> operator <<(Action<T1, T2, T3> action, T1 obj)
            => action.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Func{T1, T2, T3, T4, TResult}"/>.
    /// </summary>
    /// <param name="receiver">The function to bind.</param>
    /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    extension<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, TResult> receiver)
        where T1 : class
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Func<T2, T3, T4, TResult> Bind(T1 obj)
            => Bind(receiver, obj, &Closure<T1>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="func">The function to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Func<T2, T3, T4, TResult> operator <<(Func<T1, T2, T3, T4, TResult> func, T1 obj)
            => func.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Action{T1, T2, T3, T4}"/>.
    /// </summary>
    /// <param name="receiver">The action to bind.</param>
    /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    extension<T1, T2, T3, T4>(Action<T1, T2, T3, T4> receiver)
        where T1 : class
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Action<T2, T3, T4> Bind(T1 obj)
            => Bind(receiver, obj, &Closure<T1>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Action<T2, T3, T4> operator <<(Action<T1, T2, T3, T4> action, T1 obj)
            => action.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
    /// </summary>
    /// <param name="receiver">The function to bind.</param>
    /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that the delegate encapsulates.</typeparam>
    extension<T1, T2, T3, T4, T5, TResult>(Func<T1, T2, T3, T4, T5, TResult> receiver)
        where T1 : class
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where T5 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Func<T2, T3, T4, T5, TResult> Bind(T1 obj)
            => Bind(receiver, obj, &Closure<T1>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="func">The function to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Func<T2, T3, T4, T5, TResult> operator <<(Func<T1, T2, T3, T4, T5, TResult> func, T1 obj)
            => func.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Action{T1, T2, T3, T4, T5}"/>.
    /// </summary>
    /// <param name="receiver">The action to bind.</param>
    /// <typeparam name="T1">The type of the first parameter to bind.</typeparam>
    /// <typeparam name="T2">The type of the second parameter.</typeparam>
    /// <typeparam name="T3">The type of the third parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
    extension<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> receiver)
        where T1 : class
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where T5 : allows ref struct
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Action<T2, T3, T4, T5> Bind(T1 obj)
            => Bind(receiver, obj, &Closure<T1>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="action">The action to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Action<T2, T3, T4, T5> operator <<(Action<T1, T2, T3, T4, T5> action, T1 obj)
            => action.Bind(obj);
    }

    /// <summary>
    /// Provides binding for <see cref="Predicate{T}"/>.
    /// </summary>
    /// <param name="receiver">The predicate to bind.</param>
    /// <typeparam name="T">The type of the first parameter to bind.</typeparam>
    extension<T>(Predicate<T> receiver) where T : class
    {
        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        public unsafe Func<bool> Bind(T obj)
            => Bind(receiver, obj, &Closure<T>.Create);

        /// <summary>
        /// Produces a delegate whose first parameter is implicitly bound to the given object.
        /// </summary>
        /// <param name="predicate">The predicate to bind.</param>
        /// <param name="obj">The object to be passed implicitly as the first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The delegate targeting the specified object.</returns>
        public static Func<bool> operator <<(Predicate<T> predicate, T obj)
            => predicate.Bind(obj);
    }
}