using System;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    using Reflection;

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct TaskType
    {
        private readonly Type resultType;
        private readonly Type taskType;

        internal TaskType(Type resultType, bool isValueTask)
        {
            IsValueTask = isValueTask;
            this.resultType = resultType;
            if (resultType is null || resultType == typeof(void))
                taskType = isValueTask ? typeof(ValueTask) : typeof(Task);
            else
                taskType = (isValueTask ? typeof(ValueTask<>) : typeof(Task<>)).MakeGenericType(resultType);
        }

        internal TaskType(Type taskType)
        {
            this.taskType = taskType;
            if (taskType is null)
                throw new ArgumentException(ExceptionMessages.UnsupportedAsyncType);
            else if (taskType == typeof(ValueTask))
            {
                resultType = null;
                IsValueTask = true;
            }
            else if (taskType == typeof(Task))
            {
                resultType = null;
                IsValueTask = false;
            }
            else if (taskType.IsGenericInstanceOf(typeof(Task<>)))
            {
                resultType = taskType.GetGenericArguments(typeof(Task<>))[0];
                IsValueTask = false;
            }
            else if (taskType.IsGenericInstanceOf(typeof(ValueTask<>)))
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

        public static implicit operator Type(TaskType type) => type.taskType ?? typeof(Task);
    }
}
