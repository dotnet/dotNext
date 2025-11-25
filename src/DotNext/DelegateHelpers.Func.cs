using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext;

partial class DelegateHelpers
{
    /// <summary>
    /// Represents extensions for <see cref="Func{TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to be invoked.</param>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<TResult>(Func<TResult> func)
    {
        /// <summary>
        /// Converts function to async delegate.
        /// </summary>
        /// <returns>The asynchronous function that wraps <paramref name="func"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public Func<CancellationToken, ValueTask<TResult>> ToAsync()
        {
            ArgumentNullException.ThrowIfNull(func);

            return func.Invoke;
        }

        private ValueTask<TResult> Invoke(CancellationToken token)
        {
            ValueTask<TResult> task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled<TResult>(token);
            }
            else
            {
                try
                {
                    task = new(func.Invoke());
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException<TResult>(e);
                }
            }

            return task;
        }
        
        /// <summary>
        /// Constructs <see cref="Func{T}"/> returning the same
        /// instance each call.
        /// </summary>
        /// <param name="obj">The object to be returned from the delegate.</param>
        /// <returns>The delegate returning <paramref name="obj"/> each call.</returns>
        public static Func<TResult> Constant(TResult obj)
        {
            // use cache for boolean values
            if (typeof(TResult) == typeof(bool))
                return Unsafe.As<Func<TResult>>(FromBoolConstant(Unsafe.As<TResult, bool>(ref obj)));

            // slow path - allocates a new delegate
            return obj is null ? Default<TResult?>! : obj.UnboxAny<TResult>;
        }
        
        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <returns>The invocation result.</returns>
        public Result<TResult> TryInvoke()
        {
            Result<TResult> result;
            try
            {
                result = func();
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to be invoked.</param>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<TResult>(Func<TResult> func) where TResult : allows ref struct
    {
        /// <summary>
        /// Creates a delegate that hides the return value.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <returns>The action that invokes the same method as <paramref name="func"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public Action HideReturnValue()
        {
            ArgumentNullException.ThrowIfNull(func);

            return func.InvokeNoReturn;
        }

        private void InvokeNoReturn() => func.Invoke();
        
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{TResult}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<TResult> FromPointer(delegate*<TResult> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Func<TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<TResult>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{TResult}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<TResult> FromPointer<TTarget>(delegate*<TTarget, TResult> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Func<TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<TResult>>();
        }
    }
    
    /// <summary>
    /// Represents extensions for <see cref="Func{T, TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to be invoked.</param>
    /// <typeparam name="T">The type of the argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T, TResult>(Func<T, TResult> func)
        where T : allows ref struct
    {
        /// <summary>
        /// Converts function to async delegate.
        /// </summary>
        /// <returns>The asynchronous function that wraps <paramref name="func"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public Func<T, CancellationToken, ValueTask<TResult>> ToAsync()
        {
            ArgumentNullException.ThrowIfNull(func);

            return func.Invoke;
        }

        private ValueTask<TResult> Invoke(T arg, CancellationToken token)
        {
            ValueTask<TResult> task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled<TResult>(token);
            }
            else
            {
                try
                {
                    task = new(func.Invoke(arg));
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException<TResult>(e);
                }
            }

            return task;
        }
        
        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <param name="arg">The first function argument.</param>
        /// <returns>The invocation result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TResult> TryInvoke(T arg)
        {
            Result<TResult> result;
            try
            {
                result = func(arg);
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }

        /// <summary>
        /// Constructs <see cref="Func{T, TResult}"/> returning the same
        /// instance each call.
        /// </summary>
        /// <param name="obj">The object to be returned from the delegate.</param>
        /// <returns>The delegate returning <paramref name="obj"/> each call.</returns>
        public static Func<T, TResult> Constant(TResult obj)
        {
            // use cache for boolean values
            if (typeof(TResult) == typeof(bool))
                return Unsafe.As<Func<T, TResult>>(FromBoolConstant<T>(Unsafe.As<TResult, bool>(ref obj)));

            // slow path - allocates a new delegate
            return obj is null ? Default<T, TResult?>! : obj.UnboxAny<T, TResult>;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T, TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to be invoked.</param>
    /// <typeparam name="T">The type of the argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T, TResult>(Func<T, TResult> func)
        where T : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Creates a delegate that hides the return value.
        /// </summary>
        /// <returns>The action that invokes the same method as <paramref name="func"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public  Action<T> HideReturnValue()
        {
            ArgumentNullException.ThrowIfNull(func);

            return func.InvokeNoReturn;
        }

        private void InvokeNoReturn(T arg) => func.Invoke(arg);

        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T, TResult> FromPointer(delegate*<T, TResult> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Func<T, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T, TResult>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="obj">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T, TResult> FromPointer<TTarget>(delegate*<TTarget, T, TResult> ptr, TTarget obj)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(obj);
            Push(ptr);
            Newobj(Constructor(Type<Func<T, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T, TResult>>();
        }
    }

    /// <summary>
    /// Represents identity extensions.
    /// </summary>
    /// <typeparam name="T">The type of the function parameter and the return type.</typeparam>
    extension<T>(Func<T, T>) where T : allows ref struct
    {
        /// <summary>
        /// Returns the input argument.
        /// </summary>
        /// <param name="arg">The argument to be returned.</param>
        /// <returns>The same as <paramref name="arg"/>.</returns>
        public static T Identity(T arg) => arg;
    }

    /// <summary>
    /// Represents identity extensions.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    extension<TInput, TOutput>(Func<TInput, TOutput>) where TInput : TOutput
    {
        /// <summary>
        /// Returns the input argument.
        /// </summary>
        /// <param name="arg">The argument to be returned.</param>
        /// <returns>The same as <paramref name="arg"/>.</returns>
        public static TOutput Convert(TInput arg) => arg;
    }

    /// <summary>
    /// Provides extension operators for <see cref="Func{T, TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to extend.</param>
    /// <typeparam name="T">The input type of the predicate.</typeparam>
    extension<T>(Func<T, bool> func) where T : allows ref struct
    {
        /// <summary>
        /// Returns a predicate which negates evaluation result of
        /// the original predicate.
        /// </summary>
        /// <typeparam name="T">Type of the predicate argument.</typeparam>
        /// <param name="other">The predicate to negate.</param>
        /// <returns>The predicate which negates evaluation result of the original predicate.</returns>
        public static Predicate<T> operator !(Func<T, bool> other)
        {
            ArgumentNullException.ThrowIfNull(other);

            return other.Negate;
        }

        private bool Negate(T obj) => !func(obj);

        /// <summary>
        /// Returns a predicate which computes logical AND between
        /// results of two other predicates.
        /// </summary>
        /// <param name="x">The first predicate acting as logical AND operand.</param>
        /// <param name="y">The second predicate acting as logical AND operand.</param>
        /// <returns>The predicate which computes logical AND between results of two other predicates.</returns>
        public static Predicate<T> operator &(Func<T, bool> x, Func<T, bool> y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            return new BinaryOperator<T>(x, y).And;
        }

        /// <summary>
        /// Returns a predicate which computes logical OR between
        /// results of two other predicates.
        /// </summary>
        /// <param name="x">The first predicate acting as logical OR operand.</param>
        /// <param name="y">The second predicate acting as logical OR operand.</param>
        /// <returns>The predicate which computes logical OR between results of two other predicates.</returns>
        public static Predicate<T> operator |(Func<T, bool> x, Func<T, bool> y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            return new BinaryOperator<T>(x, y).Or;
        }

        /// <summary>
        /// Returns a predicate which computes logical XOR between
        /// results of two other predicates.
        /// </summary>
        /// <param name="x">The first predicate acting as logical XOR operand.</param>
        /// <param name="y">The second predicate acting as logical XOR operand.</param>
        /// <returns>The predicate which computes logical XOR between results of two other predicates.</returns>
        public static Predicate<T> operator ^(Func<T, bool> x, Func<T, bool> y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            return new BinaryOperator<T>(x, y).Xor;
        }

        /// <summary>
        /// Converts <see cref="Func{T, Boolean}"/> into predicate.
        /// </summary>
        /// <returns>A delegate of type <see cref="Predicate{T}"/> referencing the same method as original delegate.</returns>
        public Predicate<T> AsPredicate()
            => func.ChangeType<Predicate<T>>();
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to be invoked.</param>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, TResult>(Func<T1, T2, TResult> func)
        where T1 : allows ref struct
        where T2 : allows ref struct
    {
        /// <summary>
        /// Converts function to async delegate.
        /// </summary>
        /// <returns>The asynchronous function that wraps <paramref name="func"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public Func<T1, T2, CancellationToken, ValueTask<TResult>> ToAsync()
        {
            ArgumentNullException.ThrowIfNull(func);

            return func.Invoke;
        }

        private ValueTask<TResult> Invoke(T1 arg1, T2 arg2, CancellationToken token)
        {
            ValueTask<TResult> task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled<TResult>(token);
            }
            else
            {
                try
                {
                    task = new(func.Invoke(arg1, arg2));
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException<TResult>(e);
                }
            }

            return task;
        }
        
        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <returns>The invocation result.</returns>
        public Result<TResult> TryInvoke(T1 arg1, T2 arg2)
        {
            Result<TResult> result;
            try
            {
                result = func(arg1, arg2);
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, TResult}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, TResult>(Func<T1, T2, TResult>)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, TResult> FromPointer(delegate*<T1, T2, TResult> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, TResult>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, TResult> FromPointer<TTarget>(delegate*<TTarget, T1, T2, TResult> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, TResult>>();
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, T3, TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to extend.</param>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> func)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
    {
        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <returns>The invocation result.</returns>
        public Result<TResult> TryInvoke(T1 arg1, T2 arg2, T3 arg3)
        {
            Result<TResult> result;
            try
            {
                result = func(arg1, arg2, arg3);
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, T3, TResult}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult>)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, T3, TResult> FromPointer(delegate*<T1, T2, T3, TResult> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, T3, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, T3, TResult>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, T3, TResult> FromPointer<TTarget>(delegate*<TTarget, T1, T2, T3, TResult> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, T3, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, T3, TResult>>();
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, T3, T4, TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to extend.</param>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, TResult> func)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
    {
        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <returns>The invocation result.</returns>
        public Result<TResult> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            Result<TResult> result;
            try
            {
                result = func(arg1, arg2, arg3, arg4);
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, T3, T4, TResult}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, TResult>)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, T3, T4, TResult> FromPointer(delegate*<T1, T2, T3, T4, TResult> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, T3, T4, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, T3, T4, TResult>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, T3, T4, TResult> FromPointer<TTarget>(delegate*<TTarget, T1, T2, T3, T4, TResult> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, T3, T4, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, T3, T4, TResult>>();
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, T4, T4, T5, TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to extend.</param>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument to be passed to the function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, T3, T4, T5, TResult>(Func<T1, T2, T3, T4, T5, TResult> func)
    {
        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <returns>The invocation result.</returns>
        public Result<TResult> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            Result<TResult> result;
            try
            {
                result = func(arg1, arg2, arg3, arg4, arg5);
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, T4, T4, T5, TResult}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument to be passed to the function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, T3, T4, T5, TResult>(Func<T1, T2, T3, T4, T5, TResult>)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where T5 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, T3, T4, T5, TResult> FromPointer(delegate*<T1, T2, T3, T4, T5, TResult> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, T3, T4, T5, TResult>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, T3, T4, T5, TResult> FromPointer<TTarget>(delegate*<TTarget, T1, T2, T3, T4, T5, TResult> ptr,
            TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, T3, T4, T5, TResult>>();
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, T4, T4, T5, T6, TResult}"/> type.
    /// </summary>
    /// <param name="func">The function to extend.</param>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument to be passed to the function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument to be passed to the function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, T3, T4, T5, T6, TResult>(Func<T1, T2, T3, T4, T5, T6, TResult> func)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where T5 : allows ref struct
        where T6 : allows ref struct
    {
        /// <summary>
        /// Invokes function without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first function argument.</param>
        /// <param name="arg2">The second function argument.</param>
        /// <param name="arg3">The third function argument.</param>
        /// <param name="arg4">The fourth function argument.</param>
        /// <param name="arg5">The fifth function argument.</param>
        /// <param name="arg6">The sixth function argument.</param>
        /// <returns>The invocation result.</returns>
        public Result<TResult> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            Result<TResult> result;
            try
            {
                result = func(arg1, arg2, arg3, arg4, arg5, arg6);
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Func{T1, T2, T4, T4, T5, T6, TResult}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument to be passed to the function.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed to the function.</typeparam>
    /// <typeparam name="T3">The type of the third argument to be passed to the function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument to be passed to the function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument to be passed to the function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument to be passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    extension<T1, T2, T3, T4, T5, T6, TResult>(Func<T1, T2, T3, T4, T5, T6, TResult>)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where T5 : allows ref struct
        where T6 : allows ref struct
        where TResult : allows ref struct
    {
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Func{T1, T2, T3, T4, T5, T6, TResult}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, T3, T4, T5, T6, TResult> FromPointer(
            delegate*<T1, T2, T3, T4, T5, T6, TResult> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, T6, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, T3, T4, T5, T6, TResult>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Func{T1, T2, T3, T4, T5, T6, TResult}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Func<T1, T2, T3, T4, T5, T6, TResult> FromPointer<TTarget>(
            delegate*<TTarget, T1, T2, T3, T4, T5, T6, TResult> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, T6, TResult>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<T1, T2, T3, T4, T5, T6, TResult>>();
        }
    }
}

file sealed class BinaryOperator<T>(Func<T, bool> left, Func<T, bool> right)
    where T : allows ref struct
{
    internal bool Or(T value) => left(value) || right(value);

    internal bool And(T value) => left(value) && right(value);

    internal bool Xor(T value) => left(value) ^ right(value);
}