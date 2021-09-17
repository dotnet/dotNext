using System.Buffers;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext;

public static partial class DelegateHelpers
{
    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="RefAction{T, TArgs}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to be passed by reference into the action.</typeparam>
    /// <typeparam name="TArgs">The type of the arguments to be passed into the action.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe RefAction<T, TArgs> CreateDelegate<T, TArgs>(delegate*<ref T, TArgs, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<RefAction<T, TArgs>>(), Type<object>(), Type<IntPtr>()));
        return Return<RefAction<T, TArgs>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="RefAction{T, TArgs}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="TRef">The type of the object to be passed by reference into the action.</typeparam>
    /// <typeparam name="TArgs">The type of the arguments to be passed into the action.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe RefAction<TRef, TArgs> CreateDelegate<T, TRef, TArgs>(delegate*<T, ref TRef, TArgs, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<RefAction<TRef, TArgs>>(), Type<object>(), Type<IntPtr>()));
        return Return<RefAction<TRef, TArgs>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="RefFunc{T, TArgs, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to be passed by reference into the action.</typeparam>
    /// <typeparam name="TArgs">The type of the arguments to be passed into the action.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe RefFunc<T, TArgs, TResult> CreateDelegate<T, TArgs, TResult>(delegate*<ref T, TArgs, TResult> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<RefFunc<T, TArgs, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<RefFunc<T, TArgs, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="RefAction{T, TArgs}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="TRef">The type of the object to be passed by reference into the action.</typeparam>
    /// <typeparam name="TArgs">The type of the arguments to be passed into the action.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe RefFunc<TRef, TArgs, TResult> CreateDelegate<T, TRef, TArgs, TResult>(delegate*<T, ref TRef, TArgs, TResult> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<RefFunc<TRef, TArgs, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<RefFunc<TRef, TArgs, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Action"/>.
    /// </summary>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action CreateDelegate(delegate*<void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Action>(), Type<object>(), Type<IntPtr>()));
        return Return<Action>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action CreateDelegate<T>(delegate*<T, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Action>(), Type<object>(), Type<IntPtr>()));
        return Return<Action>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the first argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T> CreateDelegate<T>(delegate*<T, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Action<T>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1> CreateDelegate<T, T1>(delegate*<T, T1, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Action<T1>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<TResult> CreateDelegate<TResult>(delegate*<TResult> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Func<TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<TResult> CreateDelegate<T, TResult>(delegate*<T, TResult> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Func<TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2> CreateDelegate<T1, T2>(delegate*<T1, T2, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2> CreateDelegate<T, T1, T2>(delegate*<T, T1, T2, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2>>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe Converter<TInput, TOutput> CreateConverter<TInput, TOutput>(delegate*<TInput, TOutput> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Converter<TInput, TOutput>>(), Type<object>(), Type<IntPtr>()));
        return Return<Converter<TInput, TOutput>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the first argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T, TResult> CreateDelegate<T, TResult>(delegate*<T, TResult> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Func<T, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="TArg">The type of the first argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<TArg, TResult> CreateDelegate<T, TArg, TResult>(delegate*<T, TArg, TResult> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Func<TArg, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<TArg, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2, T3}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2, T3> CreateDelegate<T1, T2, T3>(delegate*<T1, T2, T3, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2, T3>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2, T3>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2, T3}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2, T3> CreateDelegate<T, T1, T2, T3>(delegate*<T, T1, T2, T3, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2, T3>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2, T3>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, TResult}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, TResult> CreateDelegate<T1, T2, TResult>(delegate*<T1, T2, TResult> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, TResult> CreateDelegate<T, T1, T2, TResult>(delegate*<T, T1, T2, TResult> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2, T3, T4}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2, T3, T4> CreateDelegate<T1, T2, T3, T4>(delegate*<T1, T2, T3, T4, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2, T3, T4>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2, T3, T4>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2, T3, T4}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2, T3, T4> CreateDelegate<T, T1, T2, T3, T4>(delegate*<T, T1, T2, T3, T4, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2, T3, T4>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2, T3, T4>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, T3, TResult}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, T3, TResult> CreateDelegate<T1, T2, T3, TResult>(delegate*<T1, T2, T3, TResult> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, T3, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, T3, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, T3, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, T3, TResult> CreateDelegate<T, T1, T2, T3, TResult>(delegate*<T, T1, T2, T3, TResult> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, T3, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, T3, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2, T3, T4, T5}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2, T3, T4, T5> CreateDelegate<T1, T2, T3, T4, T5>(delegate*<T1, T2, T3, T4, T5, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2, T3, T4, T5>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2, T3, T4, T5}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2, T3, T4, T5> CreateDelegate<T, T1, T2, T3, T4, T5>(delegate*<T, T1, T2, T3, T4, T5, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2, T3, T4, T5>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, T3, T4, TResult}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, T3, T4, TResult> CreateDelegate<T1, T2, T3, T4, TResult>(delegate*<T1, T2, T3, T4, TResult> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, T3, T4, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, T3, T4, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, T3, T4, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, T3, T4, TResult> CreateDelegate<T, T1, T2, T3, T4, TResult>(delegate*<T, T1, T2, T3, T4, TResult> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, T3, T4, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, T3, T4, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2, T3, T4, T5, T6}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2, T3, T4, T5, T6> CreateDelegate<T1, T2, T3, T4, T5, T6>(delegate*<T1, T2, T3, T4, T5, T6, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5, T6>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2, T3, T4, T5, T6>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2, T3, T4, T5, T6}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Action<T1, T2, T3, T4, T5, T6> CreateDelegate<T, T1, T2, T3, T4, T5, T6>(delegate*<T, T1, T2, T3, T4, T5, T6, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5, T6>>(), Type<object>(), Type<IntPtr>()));
        return Return<Action<T1, T2, T3, T4, T5, T6>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, T3, T4, T5, TResult> CreateDelegate<T1, T2, T3, T4, T5, TResult>(delegate*<T1, T2, T3, T4, T5, TResult> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, T3, T4, T5, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, T3, T4, T5, TResult> CreateDelegate<T, T1, T2, T3, T4, T5, TResult>(delegate*<T, T1, T2, T3, T4, T5, TResult> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, T3, T4, T5, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, T3, T4, T5, T6, TResult}"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, T3, T4, T5, T6, TResult> CreateDelegate<T1, T2, T3, T4, T5, T6, TResult>(delegate*<T1, T2, T3, T4, T5, T6, TResult> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, T6, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, T3, T4, T5, T6, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, T3, T4, T5, T6, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <typeparam name="T3">The type of the third argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe Func<T1, T2, T3, T4, T5, T6, TResult> CreateDelegate<T, T1, T2, T3, T4, T5, T6, TResult>(delegate*<T, T1, T2, T3, T4, T5, T6, TResult> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, T6, TResult>>(), Type<object>(), Type<IntPtr>()));
        return Return<Func<T1, T2, T3, T4, T5, T6, TResult>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="ReadOnlySpanAction{T, TArg}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the objects in the read-only span.</typeparam>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe ReadOnlySpanAction<T, TArg> CreateDelegate<T, TArg>(delegate*<ReadOnlySpan<T>, TArg, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<ReadOnlySpanAction<T, TArg>>(), Type<object>(), Type<IntPtr>()));
        return Return<ReadOnlySpanAction<T, TArg>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="ReadOnlySpanAction{T, TArg}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="TItem">The type of the objects in the read-only span.</typeparam>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe ReadOnlySpanAction<TItem, TArg> CreateDelegate<T, TItem, TArg>(delegate*<T, ReadOnlySpan<TItem>, TArg, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<ReadOnlySpanAction<TItem, TArg>>(), Type<object>(), Type<IntPtr>()));
        return Return<ReadOnlySpanAction<TItem, TArg>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the open delegate of type <see cref="SpanAction{T, TArg}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the objects in the read-only span.</typeparam>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe SpanAction<T, TArg> CreateDelegate<T, TArg>(delegate*<Span<T>, TArg, void> ptr)
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Ldnull();
        Push(ptr);
        Newobj(Constructor(Type<SpanAction<T, TArg>>(), Type<object>(), Type<IntPtr>()));
        return Return<SpanAction<T, TArg>>();
    }

    /// <summary>
    /// Converts static method represented by the pointer to the closed delegate of type <see cref="SpanAction{T, TArg}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the implicitly capture object.</typeparam>
    /// <typeparam name="TItem">The type of the objects in the read-only span.</typeparam>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="obj">The object to be passed as first argument implicitly.</param>
    /// <returns>The delegate instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe SpanAction<TItem, TArg> CreateDelegate<T, TItem, TArg>(delegate*<T, Span<TItem>, TArg, void> ptr, T obj)
        where T : class?
    {
        if (ptr == null)
            throw new ArgumentNullException(nameof(ptr));

        Push(obj);
        Push(ptr);
        Newobj(Constructor(Type<SpanAction<TItem, TArg>>(), Type<object>(), Type<IntPtr>()));
        return Return<SpanAction<TItem, TArg>>();
    }
}