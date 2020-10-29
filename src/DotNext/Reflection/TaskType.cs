using System;
using System.Threading.Tasks;

namespace DotNext.Reflection
{
    using Runtime.CompilerServices;

    /// <summary>
    /// Provides specialized reflection methods for
    /// task types.
    /// </summary>
    /// <seealso cref="Task"/>
    /// <seealso cref="Task{TResult}"/>
    public static class TaskType
    {
        /// <summary>
        /// Returns task type for the specified result type.
        /// </summary>
        /// <param name="taskResult">Task result type.</param>
        /// <returns>Returns <see cref="Task"/> if <paramref name="taskResult"/> is <see cref="Void"/>; or <see cref="Task{TResult}"/> with actual generic argument equals to <paramref name="taskResult"/>.</returns>
        /// <seealso cref="Task"/>
        /// <seealso cref="Task{TResult}"/>
        [RuntimeFeatures(RuntimeGenericInstantiation = true)]
        public static Type MakeTaskType(this Type taskResult)
            => MakeTaskType(taskResult, false);

        /// <summary>
        /// Returns task type for the specified result type.
        /// </summary>
        /// <param name="taskResult">Task result type.</param>
        /// <param name="valueTask"><see langword="true"/> to make value task type.</param>
        /// <returns>Returns <see cref="Task"/> or <see cref="ValueTask"/> if <paramref name="taskResult"/> is <see cref="Void"/>; or <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/> with actual generic argument equals to <paramref name="taskResult"/>.</returns>
        /// <seealso cref="Task"/>
        /// <seealso cref="Task{TResult}"/>
        /// <seealso cref="ValueTask"/>
        /// <seealso cref="ValueTask{TResult}"/>
        [RuntimeFeatures(RuntimeGenericInstantiation = true)]
        public static Type MakeTaskType(this Type taskResult, bool valueTask)
        {
            if (taskResult == typeof(void))
                return valueTask ? typeof(ValueTask) : typeof(Task);

            return (valueTask ? typeof(ValueTask<>) : typeof(Task<>)).MakeGenericType(taskResult);
        }

        private static Type? GetValueTaskType(Type valueTaskType)
        {
            if (valueTaskType == typeof(ValueTask))
                return typeof(void);

            if (valueTaskType.IsConstructedGenericType && valueTaskType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                return valueTaskType.GetGenericArguments()[0];

            return null;
        }

        /// <summary>
        /// Obtains result type from task type.
        /// </summary>
        /// <param name="taskType">A type of <see cref="Task"/> or <see cref="Task{TResult}"/>.</param>
        /// <returns>Task result type; or <see langword="null"/> if <paramref name="taskType"/> is not a task type.</returns>
        public static Type? GetTaskType(this Type taskType)
        {
            if (taskType.IsValueType)
                return GetValueTaskType(taskType);

            var result = taskType.FindGenericInstance(typeof(Task<>));
            if (!(result is null))
                return result.GetGenericArguments()[0];

            if (typeof(Task).IsAssignableFrom(taskType))
                return typeof(void);

            return null;
        }
    }
}