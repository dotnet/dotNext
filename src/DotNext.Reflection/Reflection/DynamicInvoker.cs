using System;
using System.Runtime.InteropServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents invoker of a member.
    /// </summary>
    /// <remarks>
    /// Arguments dependending on the member:
    /// <list type="bullet">
    /// <listheader>
    /// <term>Field</term>
    /// <description>Read operation doesn't require arguments; Write operation requires single argument with field value.</description>
    /// </listheader>
    /// <listheader>
    /// <term>Method</term>
    /// <description>Arguments will be passed to the method as-is.</description>
    /// </listheader>
    /// </list>
    /// </remarks>
    /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The result of member invocation.</returns>
    public delegate object? DynamicInvoker(object? target, Span<object?> args);

    /// <summary>
    /// Represents various extensions of <see cref="DynamicInvoker"/> delegate.
    /// </summary>
    public static class DynamicInvokerExtensions
    {
        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="invoker">The delegate to invoke.</param>
        /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
        /// <returns>The result of member invocation.</returns>
        public static object? Invoke(this DynamicInvoker invoker, object? target)
            => invoker(target, Span<object?>.Empty);

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="invoker">The delegate to invoke.</param>
        /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
        /// <param name="arg">The first argument.</param>
        /// <returns>The result of member invocation.</returns>
        public static object? Invoke(this DynamicInvoker invoker, object? target, object? arg)
            => invoker(target, MemoryMarshal.CreateSpan(ref arg, 1));

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="invoker">The delegate to invoke.</param>
        /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        /// <returns>The result of member invocation.</returns>
        public static object? Invoke(this DynamicInvoker invoker, object? target, object? arg1, object? arg2)
        {
            var args = (arg1, arg2);
            return invoker(target, Span.AsSpan(ref args));
        }

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="invoker">The delegate to invoke.</param>
        /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        /// <param name="arg3">The third argument.</param>
        /// <returns>The result of member invocation.</returns>
        public static object? Invoke(this DynamicInvoker invoker, object? target, object? arg1, object? arg2, object? arg3)
        {
            var args = (arg1, arg2, arg3);
            return invoker(target, Span.AsSpan(ref args));
        }

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="invoker">The delegate to invoke.</param>
        /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        /// <param name="arg3">The third argument.</param>
        /// <param name="arg4">The fourth argument.</param>
        /// <returns>The result of member invocation.</returns>
        public static object? Invoke(this DynamicInvoker invoker, object? target, object? arg1, object? arg2, object? arg3, object? arg4)
        {
            var args = (arg1, arg2, arg3, arg4);
            return invoker(target, Span.AsSpan(ref args));
        }

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="invoker">The delegate to invoke.</param>
        /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        /// <param name="arg3">The third argument.</param>
        /// <param name="arg4">The fourth argument.</param>
        /// <param name="arg5">The fifth argument.</param>
        /// <returns>The result of member invocation.</returns>
        public static object? Invoke(this DynamicInvoker invoker, object? target, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5)
        {
            var args = (arg1, arg2, arg3, arg4, arg5);
            return invoker(target, Span.AsSpan(ref args));
        }

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="invoker">The delegate to invoke.</param>
        /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        /// <param name="arg3">The third argument.</param>
        /// <param name="arg4">The fourth argument.</param>
        /// <param name="arg5">The fifth argument.</param>
        /// <param name="arg6">The sixth argument.</param>
        /// <returns>The result of member invocation.</returns>
        public static object? Invoke(this DynamicInvoker invoker, object? target, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6)
        {
            var args = (arg1, arg2, arg3, arg4, arg5, arg6);
            return invoker(target, Span.AsSpan(ref args));
        }
    }
}
