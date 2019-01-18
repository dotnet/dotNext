using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

namespace DotNext
{
    /// <summary>
    /// Represents various extensions of delegates.
    /// </summary>
    public static class Delegates
    {
        /// <summary>
        /// Invokes event handlers asynchronously.
        /// </summary>
        /// <typeparam name="E">Type of event object.</typeparam>
        /// <param name="handler">A set event handlers combined as single delegate.</param>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Event arguments.</param>
        /// <returns>An object representing state of the asynchronous invocation.</returns>
        public static Task InvokeAsync<E>(this EventHandler<E> handler, object sender, E args)
        {
            Task StartNew(EventHandler<E> singleHandler) => Task.Factory.StartNew(() => singleHandler(sender, args));

            if (handler is null)
                return Task.CompletedTask;
            var handlers = handler.GetInvocationList() as EventHandler<E>[] ?? Array.Empty<EventHandler<E>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(handler);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        /// <summary>
        /// Invokes event handlers asynchronously.
        /// </summary>
        /// <param name="handler">A set event handlers combined as single delegate.</param>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Event arguments.</param>
        /// <param name="parallel"><see langword="true"/> to invoke each handler in parallel; otherwise, invoke all handlers in the separated task synchronously.</param>
        /// <returns>An object representing state of the asynchronous invocation.</returns>
        public static Task InvokeAsync(this EventHandler handler, object sender, EventArgs args, bool parallel = true)
        {
            Task StartNew(EventHandler singleHandler) => Task.Factory.StartNew(() => singleHandler(sender, args));

            if (handler is null)
                return Task.CompletedTask;
            var handlers = handler.GetInvocationList() as EventHandler[] ?? Array.Empty<EventHandler>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(handler);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync(this Action action)
        {
            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action[] ?? Array.Empty<Action>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return Task.Factory.StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(Task.Factory.StartNew));
            }
        }

        public static Task InvokeAsync<T>(this Action<T> action, T arg)
        {
            Task StartNew(Action<T> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T>[] ?? Array.Empty<Action<T>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2>(this Action<T1, T2> action, T1 arg1, T2 arg2)
        {
            Task StartNew(Action<T1, T2> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2>[] ?? Array.Empty<Action<T1, T2>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2, T3>(this Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3)
        {
            Task StartNew(Action<T1, T2, T3> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2, arg3));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2, T3>[] ?? Array.Empty<Action<T1, T2, T3>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            Task StartNew(Action<T1, T2, T3, T4> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2, arg3, arg4));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2, T3, T4>[] ?? Array.Empty<Action<T1, T2, T3, T4>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            Task StartNew(Action<T1, T2, T3, T4, T5> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2, arg3, arg4, arg5));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2, T3, T4, T5>[] ?? Array.Empty<Action<T1, T2, T3, T4, T5>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6>(this Action<T1, T2, T3, T4, T5, T6> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            Task StartNew(Action<T1, T2, T3, T4, T5, T6> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2, arg3, arg4, arg5, arg6));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2, T3, T4, T5, T6>[] ?? Array.Empty<Action<T1, T2, T3, T4, T5, T6>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7>(this Action<T1, T2, T3, T4, T5, T6, T7> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            Task StartNew(Action<T1, T2, T3, T4, T5, T6, T7> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2, arg3, arg4, arg5, arg6, arg7));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2, T3, T4, T5, T6, T7>[] ?? Array.Empty<Action<T1, T2, T3, T4, T5, T6, T7>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8>(this Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            Task StartNew(Action<T1, T2, T3, T4, T5, T6, T7, T8> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2, T3, T4, T5, T6, T7, T8>[] ?? Array.Empty<Action<T1, T2, T3, T4, T5, T6, T7, T8>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
        {
            Task StartNew(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2, T3, T4, T5, T6, T7, T8, T9>[] ?? Array.Empty<Action<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
        {
            Task StartNew(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> singleHandler) => Task.Factory.StartNew(() => singleHandler(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));

            if (action is null)
                return Task.CompletedTask;
            var handlers = action.GetInvocationList() as Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>[] ?? Array.Empty<Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return StartNew(action);
                default:
                    return Task.WhenAll(handlers.Select(StartNew));
            }
        }

        public static EventHandler<O> Contravariant<I, O>(this EventHandler<I> handler)
            where I : class
            where O : class, I
            => handler.ChangeType<EventHandler<O>>();

        public static D CreateDelegate<D>(this MethodInfo method, object target = null)
            where D : Delegate
            => (D)method.CreateDelegate(typeof(D), target);

        /// <summary>
        /// Returns special Invoke method generate for each delegate type.
        /// </summary>
        /// <typeparam name="D">Type of delegate.</typeparam>
        /// <returns>An object representing reflected method Invoke.</returns>
        public static MethodInfo GetInvokeMethod<D>()
            where D : Delegate
            => Reflection.Types.GetInvokeMethod(typeof(D));

        /// <summary>
        /// Returns a new delegate of different type which
        /// points to the same method as original delegate.
        /// </summary>
        /// <param name="d">Delegate to convert.</param>
        /// <typeparam name="D">A new delegate type.</typeparam>
        /// <returns>A method wrapped into new delegate type.</returns>
        /// <exception cref="ArgumentException">Cannot convert delegate type.</exception>
        public static D ChangeType<D>(this Delegate d)
            where D : Delegate
            => d.Method.CreateDelegate<D>(d.Target);

        public static Func<I, O> AsFunc<I, O>(this Converter<I, O> converter)
            => converter.ChangeType<Func<I, O>>();

        public static Converter<I, O> AsConverter<I, O>(this Func<I, O> function)
            => function.ChangeType<Converter<I, O>>();

        public static Func<T, bool> AsFunc<T>(this Predicate<T> predicate)
            => predicate.ChangeType<Func<T, bool>>();

        public static Predicate<T> AsPredicate<T>(this Func<T, bool> predicate)
            => predicate.ChangeType<Predicate<T>>();
    }
}
