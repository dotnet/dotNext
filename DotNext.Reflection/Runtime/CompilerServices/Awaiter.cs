using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks
{
    /// <summary>
    /// Represents awaiter pattern for type <typeparamref name="TAwaiter"/>.
    /// </summary>
    /// <typeparam name="TAwaiter">Any type implementing awaiter pattern</typeparam>
    public readonly struct Awaiter<TAwaiter, R>
        where TAwaiter: ICriticalNotifyCompletion
    {
        private delegate bool IsCompletedProperty(in TAwaiter awaiter);
        private delegate void OnCompletedMethod(in TAwaiter awaiter, Action handler);


        public static bool IsCompleted(in TAwaiter awaiter)
        {
            return false;
        }

        public static R GetResult(in TAwaiter awaiter)
        {
            return default;
        }

        public static void UnsafeOnCompleted(in TAwaiter awaiter, Action continutation)
            => (Unsafe.AsRef(in awaiter)).UnsafeOnCompleted(continutation);
        
        public static void OnCompleted(in TAwaiter awaiter, Action continutation)
            => (Unsafe.AsRef(in awaiter)).OnCompleted(continutation);
    }
}