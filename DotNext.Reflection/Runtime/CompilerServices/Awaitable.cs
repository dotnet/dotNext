using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    using Reflection;

    public abstract class AwaitableBase<TAwaiter>: IConcept<TAwaiter>
        where TAwaiter: ICriticalNotifyCompletion
    {
        private static readonly MemberGetter<TAwaiter, bool> isCompleted = Type<TAwaiter>.Property<bool>.GetGetter(nameof(TaskAwaiter.IsCompleted));

        private protected AwaitableBase() => throw new NotSupportedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompleted(in TAwaiter awaiter) => isCompleted(awaiter);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnsafeOnCompleted(in TAwaiter awaiter, Action continutation)
            => (Unsafe.AsRef(in awaiter)).UnsafeOnCompleted(continutation);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnCompleted(in TAwaiter awaiter, Action continutation)
            => (Unsafe.AsRef(in awaiter)).OnCompleted(continutation);
    }

    /// <summary>
    /// Represents awaiter pattern for type <typeparamref name="TAwaiter"/>.
    /// </summary>
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern</typeparam>
    /// <typeparam name="R">Type of asynchronous result</typeparam>
    public sealed class Awaitable<TAwaiter, R>: AwaitableBase<TAwaiter>
        where TAwaiter: ICriticalNotifyCompletion
    {
        private Awaitable() => throw new NotSupportedException();

        private static readonly MemberGetter<TAwaiter, R> getResult = Type<TAwaiter>.Method.Get<MemberGetter<TAwaiter, R>>(nameof(TaskAwaiter<R>.GetResult), MethodLookup.Instance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R GetResult(in TAwaiter awaiter) => getResult(in awaiter);
    }

    public sealed class Awaitable<TAwaiter>: AwaitableBase<TAwaiter>
        where TAwaiter: ICriticalNotifyCompletion
    {
        private delegate void GetResultMethod(in TAwaiter awaiter);

        private Awaitable() => throw new NotSupportedException();

        private static readonly GetResultMethod getResult = Type<TAwaiter>.Method.Get<GetResultMethod>(nameof(TaskAwaiter.GetResult), MethodLookup.Instance);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetResult(in TAwaiter awaiter) => getResult(in awaiter);
    }
}