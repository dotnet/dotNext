using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Provides set of methods for asynchronous invocation of various delegates.
    /// </summary>
    /// <remarks>
    /// In .NET Core, BeginInvoke and EndInvoke methods of delegate type are not supported.
    /// This class provides alternative approach which allows to invoke delegate asynchronously
    /// with full support of async/await feature.
    /// </remarks>
    /// <seealso href="https://github.com/dotnet/corefx/issues/5940"/>
    public static class AsyncDelegate
    {
        private static Task StartNew<D>(D @delegate, Action<D> invoker, CancellationToken token)
            where D: Delegate
            => Task.Factory.StartNew(() => invoker(@delegate), token);

        private static Task InvokeAsync<D>(D @delegate, Func<D, Task> invoker)
            where D: MulticastDelegate
        {
            if (@delegate is null)
                return Task.CompletedTask;
            var handlers = DelegateHelpers.GetInvocationList(@delegate);
            switch (handlers.LongLength)
            {
                case 0:
                    return Task.CompletedTask;
                case 1:
                    return invoker(handlers[0]);
                default:
                    return Task.WhenAll(handlers.Select(invoker));
            }
        }

        /// <summary>
        /// Invokes a delegate of arbitrary type asynchronously.
        /// </summary>
        /// <param name="delegate">A delegate to be invoked asynchronously.</param>
        /// <param name="invoker">Synchronous invoker of the delegate from invocation list.</param>
        /// <param name="token">Cancellation token.</param>
        /// <typeparam name="D">Type of delegate to invoke.</typeparam>
        /// <returns>A task allows to control asynchronous invocation of methods attached to the multicast delegate.</returns>
        public static Task InvokeAsync<D>(this D @delegate, Action<D> invoker, CancellationToken token = default)
            where D: MulticastDelegate
            => InvokeAsync(@delegate, h => StartNew(h, invoker, token));

        /// <summary>
        /// Invokes event handlers asynchronously.
        /// </summary>
        /// <typeparam name="E">Type of event object.</typeparam>
        /// <param name="handler">A set event handlers combined as single delegate.</param>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Event arguments.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>An object representing state of the asynchronous invocation.</returns>
        public static Task InvokeAsync<E>(this EventHandler<E> handler, object sender, E args, CancellationToken token = default)
            => InvokeAsync(handler, h => h(sender, args), token);

        /// <summary>
        /// Invokes event handlers asynchronously.
        /// </summary>
        /// <param name="handler">A set event handlers combined as single delegate.</param>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Event arguments.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>An object representing state of the asynchronous invocation.</returns>
        public static Task InvokeAsync(this EventHandler handler, object sender, EventArgs args, CancellationToken token = default)
            => InvokeAsync(handler, h => h(sender, args), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync(this Action action, CancellationToken token = default)
            => InvokeAsync(action, h => h(), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of the action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg">The action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T>(this Action<T> action, T arg, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2>(this Action<T1, T2> action, T1 arg1, T2 arg2, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <typeparam name="T3">Type of the third action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2, T3>(this Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <typeparam name="T3">Type of the third action argument.</typeparam>
        /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <typeparam name="T3">Type of the third action argument.</typeparam>
        /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
        /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="arg5">The fifth action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <typeparam name="T3">Type of the third action argument.</typeparam>
        /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
        /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
        /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="arg5">The fifth action argument.</param>
        /// <param name="arg6">The sixth action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6>(this Action<T1, T2, T3, T4, T5, T6> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <typeparam name="T3">Type of the third action argument.</typeparam>
        /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
        /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
        /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
        /// <typeparam name="T7">Type of the seventh action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="arg5">The fifth action argument.</param>
        /// <param name="arg6">The sixth action argument.</param>
        /// <param name="arg7">The seventh action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7>(this Action<T1, T2, T3, T4, T5, T6, T7> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6, arg7), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <typeparam name="T3">Type of the third action argument.</typeparam>
        /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
        /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
        /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
        /// <typeparam name="T7">Type of the seventh action argument.</typeparam>
        /// <typeparam name="T8">Type of the eighth action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="arg5">The fifth action argument.</param>
        /// <param name="arg6">The sixth action argument.</param>
        /// <param name="arg7">The seventh action argument.</param>
        /// <param name="arg8">The eighth action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8>(this Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <typeparam name="T3">Type of the third action argument.</typeparam>
        /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
        /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
        /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
        /// <typeparam name="T7">Type of the seventh action argument.</typeparam>
        /// <typeparam name="T8">Type of the eighth action argument.</typeparam>
        /// <typeparam name="T9">Type of the ninth action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="arg5">The fifth action argument.</param>
        /// <param name="arg6">The sixth action argument.</param>
        /// <param name="arg7">The seventh action argument.</param>
        /// <param name="arg8">The eighth action argument.</param>
        /// <param name="arg9">THe ninth action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9), token);

        /// <summary>
        /// Invokes action asynchronously.
        /// </summary>
        /// <typeparam name="T1">Type of the first action argument.</typeparam>
        /// <typeparam name="T2">Type of the second action argument.</typeparam>
        /// <typeparam name="T3">Type of the third action argument.</typeparam>
        /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
        /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
        /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
        /// <typeparam name="T7">Type of the seventh action argument.</typeparam>
        /// <typeparam name="T8">Type of the eighth action argument.</typeparam>
        /// <typeparam name="T9">Type of the ninth action argument.</typeparam>
        /// <typeparam name="T10">Type of the tenth action argument.</typeparam>
        /// <param name="action">The action to invoke asynchronously.</param>
        /// <param name="arg1">The first action argument.</param>
        /// <param name="arg2">The second action argument.</param>
        /// <param name="arg3">The third action argument.</param>
        /// <param name="arg4">The fourth action argument.</param>
        /// <param name="arg5">The fifth action argument.</param>
        /// <param name="arg6">The sixth action argument.</param>
        /// <param name="arg7">The seventh action argument.</param>
        /// <param name="arg8">The eighth action argument.</param>
        /// <param name="arg9">The ninth action argument.</param>
        /// <param name="arg10">The tenth action argument.</param>
        /// <param name="token">Invocation cancellation token.</param>
        /// <returns>The task representing state of asynchronous invocation.</returns>
        public static Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, CancellationToken token = default)
            => InvokeAsync(action, h => h(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10), token);
    }
}