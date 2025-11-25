using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext;

public partial class DelegateHelpers
{
    /// <summary>
    /// Represents extensions for <see cref="Action"/> type.
    /// </summary>
    /// <param name="action">The action to be invoked.</param>
    extension(Action action)
    {
        /// <summary>
        /// Invokes the action without throwing the exception.
        /// </summary>
        /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
        public Exception? TryInvoke()
        {
            var result = default(Exception);
            try
            {
                action();
            }
            catch (Exception e)
            {
                result = e;
            }

            return result;
        }
        
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Action"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action FromPointer(delegate*<void> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Action>(), Type<object>(), Type<IntPtr>()));
            return Return<Action>();
        }
        
        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action FromPointer<TTarget>(delegate*<TTarget, void> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Action>(), Type<object>(), Type<IntPtr>()));
            return Return<Action>();
        }
        
        /// <summary>
        /// Converts action to async delegate.
        /// </summary>
        /// <returns>The asynchronous function that wraps <paramref name="action"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public Func<CancellationToken, ValueTask> ToAsync()
        {
            ArgumentNullException.ThrowIfNull(action);

            return action.Invoke;
        }

        private ValueTask Invoke(CancellationToken token)
        {
            ValueTask task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled(token);
            }
            else
            {
                task = ValueTask.CompletedTask;
                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }
            }

            return task;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Action{T}"/> type.
    /// </summary>
    /// <param name="action">The action to be invoked.</param>
    /// <typeparam name="T">The type of the first action argument.</typeparam>
    extension<T>(Action<T> action) where T : allows ref struct
    {
        /// <summary>
        /// Invokes the action without throwing the exception.
        /// </summary>
        /// <param name="arg">The first action argument.</param>
        /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
        public Exception? TryInvoke(T arg)
        {
            var result = default(Exception);
            try
            {
                action(arg);
            }
            catch (Exception e)
            {
                result = e;
            }

            return result;
        }

        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T> FromPointer(delegate*<T, void> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Action<T>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T> FromPointer<TTarget>(delegate*<TTarget, T, void> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Action<T>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T>>();
        }

        /// <summary>
        /// Converts action to async delegate.
        /// </summary>
        /// <returns>The asynchronous function that wraps <paramref name="action"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public Func<T, CancellationToken, ValueTask> ToAsync()
        {
            ArgumentNullException.ThrowIfNull(action);

            return action.Invoke;
        }

        private ValueTask Invoke(T arg, CancellationToken token)
        {
            ValueTask task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled(token);
            }
            else
            {
                task = ValueTask.CompletedTask;
                try
                {
                    action.Invoke(arg);
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }
            }

            return task;
        }

        /// <summary>
        /// Represents <see cref="Action{T}"/> as <see cref="Func{T, TResult}"/> which doesn't modify the input value.
        /// </summary>
        /// <returns><see cref="Func{T, TResult}"/> that wraps <paramref name="action"/>.</returns>
        public Func<T, T> Identity => action.ReturnValue;

        private T ReturnValue(T item)
        {
            action(item);
            return item;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Action{T1, T2}"/> type.
    /// </summary>
    /// <param name="action">The action to be invoked.</param>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    extension<T1, T2>(Action<T1, T2> action)
        where T1 : allows ref struct
        where T2 : allows ref struct
    {
        /// <summary>
        /// Invokes the action without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
        public Exception? TryInvoke(T1 arg1, T2 arg2)
        {
            var result = default(Exception);
            try
            {
                action(arg1, arg2);
            }
            catch (Exception e)
            {
                result = e;
            }

            return result;
        }
        
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2> FromPointer(delegate*<T1, T2, void> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2> FromPointer<TTarget>(delegate*<TTarget, T1, T2, void> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2>>();
        }

        /// <summary>
        /// Converts action to async delegate.
        /// </summary>
        /// <returns>The asynchronous function that wraps <paramref name="action"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public Func<T1, T2, CancellationToken, ValueTask> ToAsync()
        {
            ArgumentNullException.ThrowIfNull(action);

            return action.Invoke;
        }

        private ValueTask Invoke(T1 arg1, T2 arg2, CancellationToken token)
        {
            ValueTask task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled(token);
            }
            else
            {
                task = ValueTask.CompletedTask;
                try
                {
                    action.Invoke(arg1, arg2);
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }
            }

            return task;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Action{T1, T2, T3}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    extension<T1, T2, T3>(Action<T1, T2, T3> action)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
    {
        /// <summary>
        /// Invokes the action without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
        public Exception? TryInvoke(T1 arg1, T2 arg2, T3 arg3)
        {
            var result = default(Exception);
            try
            {
                action(arg1, arg2, arg3);
            }
            catch (Exception e)
            {
                result = e;
            }

            return result;
        }

        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2, T3}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2, T3> FromPointer(delegate*<T1, T2, T3, void> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2, T3>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2, T3>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2, T3}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2, T3> FromPointer<TTarget>(delegate*<TTarget, T1, T2, T3, void> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2, T3>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2, T3>>();
        }
        
        /// <summary>
        /// Converts action to async delegate.
        /// </summary>
        /// <returns>The asynchronous function that wraps <paramref name="action"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public Func<T1, T2, T3, CancellationToken, ValueTask> ToAsync()
        {
            ArgumentNullException.ThrowIfNull(action);

            return action.Invoke;
        }

        private ValueTask Invoke(T1 arg1, T2 arg2, T3 arg3, CancellationToken token)
        {
            ValueTask task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled(token);
            }
            else
            {
                task = ValueTask.CompletedTask;
                try
                {
                    action.Invoke(arg1, arg2, arg3);
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException(e);
                }
            }

            return task;
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Action{T1, T2, T3, T4}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    extension<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
    {
        /// <summary>
        /// Invokes the action without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
        public Exception? TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            var result = default(Exception);
            try
            {
                action(arg1, arg2, arg3, arg4);
            }
            catch (Exception e)
            {
                result = e;
            }

            return result;
        }

        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2, T3, T4}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2, T3, T4> FromPointer(delegate*<T1, T2, T3, T4, void> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2, T3, T4>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2, T3, T4>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2, T3, T4}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2, T3, T4> FromPointer<TTarget>(delegate*<TTarget, T1, T2, T3, T4, void> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2, T3, T4>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2, T3, T4>>();
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Action{T1, T2, T3, T4, T5}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    extension<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action)
        where T1 :allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where T5 : allows ref struct
    {
        /// <summary>
        /// Invokes the action without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="arg5">The fifth action argument.</param>
        /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
        public Exception? TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            var result = default(Exception);
            try
            {
                action(arg1, arg2, arg3, arg4, arg5);
            }
            catch (Exception e)
            {
                result = e;
            }

            return result;
        }

        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2, T3, T4, T5> FromPointer(delegate*<T1, T2, T3, T4, T5, void> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2, T3, T4, T5>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <typeparam name="TTarget">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="target">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2, T3, T4, T5> FromPointer<TTarget>(delegate*<TTarget, T1, T2, T3, T4, T5, void> ptr, TTarget target)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(target);
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2, T3, T4, T5>>();
        }
    }

    /// <summary>
    /// Represents extensions for <see cref="Action{T1, T2, T3, T4, T5, T6}"/> type.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <typeparam name="T3">The type of the third action argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">The type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">The type of the sixth action argument.</typeparam>
    /// <param name="action">The action to be invoked.</param>
    extension<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where T3 : allows ref struct
        where T4 : allows ref struct
        where T5 : allows ref struct
        where T6 : allows ref struct
    {
        /// <summary>
        /// Invokes the action without throwing the exception.
        /// </summary>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="arg5">The fifth action argument.</param>
        /// <param name="arg6">The sixth action argument.</param>
        /// <returns>The exception caused by <paramref name="action"/>; or <see langword="null"/>, if the delegate is called successfully.</returns>
        public Exception? TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4,
            T5 arg5, T6 arg6)
        {
            var result = default(Exception);
            try
            {
                action(arg1, arg2, arg3, arg4, arg5, arg6);
            }
            catch (Exception e)
            {
                result = e;
            }

            return result;
        }

        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="Action{T1, T2, T3, T4, T5, T6}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2, T3, T4, T5, T6> FromPointer(delegate*<T1, T2, T3, T4, T5, T6, void> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5, T6>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2, T3, T4, T5, T6>>();
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="Action{T1, T2, T3, T4, T5, T6}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="obj">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe Action<T1, T2, T3, T4, T5, T6> FromPointer<TTarget>(delegate*<TTarget, T1, T2, T3, T4, T5, T6, void> ptr,
            TTarget obj)
            where TTarget : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            Push(obj);
            Push(ptr);
            Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5, T6>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<T1, T2, T3, T4, T5, T6>>();
        }
    }
}