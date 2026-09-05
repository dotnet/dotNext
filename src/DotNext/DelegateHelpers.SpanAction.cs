using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext;

partial class DelegateHelpers
{
    /// <summary>
    /// Represents extension for <see cref="SpanAction{T,TArg}"/> type.
    /// </summary>
    /// <typeparam name="TItem">The type of the objects in the read-only span.</typeparam>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    extension<TItem, TArg>(SpanAction<TItem, TArg>)
        where TArg : allows ref struct
    {
        /// <summary>
        /// Converts static method represented by the pointer to the open delegate of type <see cref="SpanAction{T, TArg}"/>.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe SpanAction<TItem, TArg> FromPointer(delegate*<Span<TItem>, TArg, void> ptr)
        {
            ArgumentNullException.ThrowIfNull(ptr);

            return RuntimeFeature.IsDynamicCodeCompiled
                ? SpanActionHelpers<TItem, TArg>.Create(target: null, (nint)ptr)
                : MethodPointer.Create<TItem, TArg>(ptr);
        }

        /// <summary>
        /// Converts static method represented by the pointer to the closed delegate of type <see cref="SpanAction{T, TArg}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the implicit capture object.</typeparam>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="obj">The object to be passed as first argument implicitly.</param>
        /// <returns>The delegate instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe SpanAction<TItem, TArg> FromPointer<T>(delegate*<T, Span<TItem>, TArg, void> ptr, T obj)
            where T : class?
        {
            ArgumentNullException.ThrowIfNull(ptr);

            return RuntimeFeature.IsDynamicCodeCompiled
                ? SpanActionHelpers<TItem, TArg>.Create(obj, (nint)ptr)
                : MethodPointer<T>.Create<TItem, TArg>(ptr, obj);
        }
    }

    unsafe partial class MethodPointer
    {
        public static SpanAction<TItem, TArg> Create<TItem, TArg>(delegate*<Span<TItem>, TArg, void> ptr)
            where TArg : allows ref struct
            => new MethodPointer(ptr).Invoke;

        private void Invoke<TItem, TArg>(Span<TItem> span, TArg arg)
            where TArg : allows ref struct
            => ((delegate*<Span<TItem>, TArg, void>)pointer)(span, arg);
    }

    unsafe partial class MethodPointer<TTarget>
    {
        public static SpanAction<TItem, TArg> Create<TItem, TArg>(delegate*<TTarget, Span<TItem>, TArg, void> ptr, TTarget target)
            where TArg : allows ref struct
            => new MethodPointer<TTarget>(ptr, target).Invoke;

        private void Invoke<TItem, TArg>(Span<TItem> span, TArg arg)
            where TArg : allows ref struct
            => ((delegate*<TTarget, Span<TItem>, TArg, void>)pointer)(target, span, arg);
    }
}

file static class SpanActionHelpers<TItem, TArg>
    where TArg : allows ref struct
{
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    public static extern SpanAction<TItem, TArg> Create(object? target, nint pointer);
}