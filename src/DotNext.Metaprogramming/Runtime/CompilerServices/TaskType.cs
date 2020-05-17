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
        private readonly Type? resultType;
        private readonly Type taskType;

        internal TaskType(Type? resultType, bool isValueTask)
        {
            this.resultType = resultType;
            if (resultType is null || resultType == typeof(void))
                taskType = isValueTask ? typeof(ValueTask) : typeof(Task);
            else
                taskType = (isValueTask ? typeof(ValueTask<>) : typeof(Task<>)).MakeGenericType(resultType);
        }

        internal TaskType(Type taskType)
        {
            this.taskType = taskType;
            if (taskType.IsOneOf(typeof(ValueTask), typeof(Task)))
            {
                resultType = null;
            }
            else
            {
                using (var supportedTasks = (typeof(Task<>), typeof(ValueTask<>)).AsEnumerable().GetEnumerator())
                {
                    move_next:
                    if (!supportedTasks.MoveNext())
                        throw new ArgumentException(ExceptionMessages.UnsupportedAsyncType);
                    if (taskType.IsGenericInstanceOf(supportedTasks.Current))
                        resultType = taskType.GetGenericArguments(supportedTasks.Current)[0];
                    else
                        goto move_next;
                }
            }
        }

        internal MethodCallExpression AdjustTaskType(MethodCallExpression startMachineCall)
            => IsValueTask ? startMachineCall : Expression.Call(startMachineCall, nameof(ValueTask.AsTask), Array.Empty<Type>());

        internal Type ResultType => resultType ?? typeof(void);

        internal bool HasResult => !(resultType is null);

        internal bool IsValueTask => taskType?.IsValueType ?? false;

        public static implicit operator Type(in TaskType type) => type.taskType ?? typeof(Task);
    }
}
