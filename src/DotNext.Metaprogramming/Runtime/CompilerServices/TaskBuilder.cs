using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    using Reflection;

    internal readonly struct TaskBuilder
    {
        private readonly Type resultType;

        internal TaskBuilder(Type taskType)
        {
            if(taskType is null)
                throw new ArgumentException(ExceptionMessages.UnsupportedAsyncType);
            else if(taskType == typeof(ValueTask))
            {
                resultType = null;
                IsValueTask = true;
            }
            else if(taskType == typeof(Task))
            {
                resultType = null;
                IsValueTask = false;
            }
            else if(taskType.IsGenericInstanceOf(typeof(Task<>)))
            {
                resultType = taskType.GetGenericArguments(typeof(Task<>))[0];
                IsValueTask = false;
            }
            else if(taskType.IsGenericInstanceOf(typeof(ValueTask<>)))
            {
                resultType = taskType.GetGenericArguments(typeof(ValueTask<>))[0];
                IsValueTask = true;
            }
            else
                throw new ArgumentException(ExceptionMessages.UnsupportedAsyncType);
        }

        internal MethodCallExpression AdjustTaskType(MethodCallExpression startMachineCall)
            => IsValueTask ? startMachineCall : Expression.Call(startMachineCall, nameof(ValueTask.AsTask), Array.Empty<Type>());

        internal Type ResultType => resultType ?? typeof(void);

        internal bool IsValueTask { get; }
    }
}
