using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace DotNext.Runtime.CompilerServices
{
#if NETSTANDARD2_1
    internal static class Awaiter<TAwaiter>
#else
    internal static class Awaiter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TAwaiter>
#endif
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
                IsCompleted = isCompletedProperty.GetMethod?.CreateDelegate<IsCompletedGetter>() ?? NotCompleted;
            }
            else
            {
                var awaiterParam = Parameter(awaiterType.MakeByRefType());
                IsCompleted = Lambda<IsCompletedGetter>(Property(awaiterParam, isCompletedProperty), true, awaiterParam).Compile();
            }
        }
    }
}