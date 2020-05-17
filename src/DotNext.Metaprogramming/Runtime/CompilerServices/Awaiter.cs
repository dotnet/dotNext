using System;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace DotNext.Runtime.CompilerServices
{
    internal static class Awaiter<TAwaiter>
        where TAwaiter : INotifyCompletion
    {
        internal delegate bool IsCompletedGetter(ref TAwaiter awaiter);

        internal static readonly IsCompletedGetter IsCompleted;

        private static bool NotCompleted(ref TAwaiter awaiter) => throw new MissingMemberException(awaiter.GetType().FullName, nameof(IsCompleted));

        static Awaiter()
        {
            var awaiterType = typeof(TAwaiter);
            var isCompletedProperty = awaiterType.GetProperty(nameof(TaskAwaiter.IsCompleted), typeof(bool));
            if (isCompletedProperty is null)
            {
                IsCompleted = NotCompleted;
            }
            else if (awaiterType.IsValueType)
            {
                IsCompleted = isCompletedProperty.GetMethod.CreateDelegate<IsCompletedGetter>();
            }
            else
            {
                var awaiterParam = Parameter(awaiterType.MakeByRefType());
                IsCompleted = Lambda<IsCompletedGetter>(Property(awaiterParam, isCompletedProperty), true, awaiterParam).Compile();
            }
        }
    }
}