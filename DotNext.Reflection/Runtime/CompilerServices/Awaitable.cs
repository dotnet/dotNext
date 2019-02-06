using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    using Reflection;

    public static class Awaitable<T, [Constraint(typeof(Awaiter<>))] TAwaiter>
        where TAwaiter: ICriticalNotifyCompletion
    {
        private static readonly Operator<T, TAwaiter> getAwaiter = Type<T>.Method.Require<Operator<T, TAwaiter>>(nameof(Task.GetAwaiter), MethodLookup.Instance);

        static Awaitable() => Type<TAwaiter>.Concept(typeof(Awaiter<TAwaiter>));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TAwaiter GetAwaiter(in T obj) => getAwaiter(in obj);
    }

    public static class Awaitable<T, [Constraint(typeof(Awaiter<,>))] TAwaiter, R>
        where TAwaiter : ICriticalNotifyCompletion
    {
        private static readonly Operator<T, TAwaiter> getAwaiter = Type<T>.Method.Require<Operator<T, TAwaiter>>(nameof(Task.GetAwaiter), MethodLookup.Instance);

        static Awaitable() => Type<TAwaiter>.Concept(typeof(Awaiter<TAwaiter, R>));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TAwaiter GetAwaiter(in T obj) => getAwaiter(in obj);
    }
}