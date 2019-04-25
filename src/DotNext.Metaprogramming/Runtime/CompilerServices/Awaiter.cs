using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace DotNext.Runtime.CompilerServices
{
    internal static class Awaiter<TAwaiter>
        where TAwaiter : INotifyCompletion
    {
        internal delegate bool IsCompletedChecker(ref TAwaiter awaiter);

        internal static readonly IsCompletedChecker IsCompleted;

        private static bool NotCompleted(ref TAwaiter awaiter) => false;

        static Awaiter()
        {
            var awaiterType = typeof(TAwaiter);
            var isCompletedProperty = awaiterType.GetProperty(nameof(TaskAwaiter.IsCompleted), typeof(bool));
            if (isCompletedProperty is null)
                IsCompleted = NotCompleted;
            else if (awaiterType.IsValueType)
                IsCompleted = isCompletedProperty.GetMethod.CreateDelegate<IsCompletedChecker>();
            else
            {
                var awaiterParam = Parameter(awaiterType.MakeByRefType());
                IsCompleted = Lambda<IsCompletedChecker>(Property(awaiterParam, isCompletedProperty), true, awaiterParam).Compile();
            }
        }
    }
}