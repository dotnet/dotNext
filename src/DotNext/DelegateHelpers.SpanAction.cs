using System.Buffers;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

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

            Ldnull();
            Push(ptr);
            Newobj(Constructor(Type<SpanAction<TItem, TArg>>(), Type<object>(), Type<IntPtr>()));
            return Return<SpanAction<TItem, TArg>>();
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

            Push(obj);
            Push(ptr);
            Newobj(Constructor(Type<SpanAction<TItem, TArg>>(), Type<object>(), Type<IntPtr>()));
            return Return<SpanAction<TItem, TArg>>();
        }
    }
}