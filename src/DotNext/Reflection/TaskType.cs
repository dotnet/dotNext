using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Reflection;

/// <summary>
/// Provides specialized reflection methods for
/// task types.
/// </summary>
/// <seealso cref="Task"/>
/// <seealso cref="Task{TResult}"/>
public static class TaskType
{
    private static class Cache<T>
    {
        internal static readonly Func<Task<T>, T> ResultGetter;

        static Cache()
        {
            Ldtoken(PropertyGet(Type<Task<T>>(), nameof(Task<T>.Result)));
            Pop(out RuntimeMethodHandle getterHandle);
            
            Ldtoken<Task<T>>();
            Pop(out RuntimeTypeHandle taskHandle);

            var getterInfo = MethodBase.GetMethodFromHandle(getterHandle, taskHandle) as MethodInfo;
            Debug.Assert(getterInfo is not null);

            ResultGetter = getterInfo.CreateDelegate<Func<Task<T>, T>>();
        }
    }

    internal static readonly Type CompletedTaskType;
    private static readonly Func<Task, bool> CompletedSuccessfullyGetter;

    static TaskType()
    {
        CompletedTaskType = Task.CompletedTask.GetType();

        Ldtoken(PropertyGet(Type<Task>(), nameof(Task.IsCompletedSuccessfully)));
        Pop(out RuntimeMethodHandle getterHandle);

        Ldtoken<Task>();
        Pop(out RuntimeTypeHandle taskHandle);

        var getterInfo = MethodBase.GetMethodFromHandle(getterHandle, taskHandle) as MethodInfo;
        Debug.Assert(getterInfo is not null);

        CompletedSuccessfullyGetter = getterInfo.CreateDelegate<Func<Task, bool>>();
    }

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
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    public static Type MakeTaskType(this Type taskResult, bool valueTask = false)
    {
        if (taskResult == typeof(void))
            return valueTask ? typeof(ValueTask) : typeof(Task);

        return (valueTask ? typeof(ValueTask<>) : typeof(Task<>)).MakeGenericType(taskResult);
    }
    
    /// <summary>
    /// Extends <see cref="Type"/> type.
    /// </summary>
    /// <param name="taskType">A type of <see cref="Task"/> or <see cref="Task{TResult}"/>.</param>
    extension([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type taskType)
    {
        /// <summary>
        /// Obtains result type from task type.
        /// </summary>
        /// <value>Task result type; or <see langword="null"/> if <paramref name="taskType"/> is not a task type.</value>
        public Type? TaskGenericArgument
        {
            get
            {
                if (taskType.IsValueType)
                    return GetValueTaskType(taskType);

                // this is workaround for .NET 5 and later
                // because Task.CompletedTask returning instance of generic type Task<TaskVoidResult>
                if (taskType == CompletedTaskType)
                    return typeof(void);

                if (taskType.FindGenericInstance(typeof(Task<>)) is { } result)
                {
                    result = result.GetGenericArguments()[0];
                }
                else if (typeof(Task).IsAssignableFrom(taskType))
                {
                    result = typeof(void);
                }
                else
                {
                    result = null;
                }

                return result;

                static Type? GetValueTaskType(Type valueTaskType)
                {
                    Type? result;

                    if (valueTaskType == typeof(ValueTask))
                    {
                        result = typeof(void);
                    }
                    else if (valueTaskType.IsConstructedGenericType && valueTaskType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                    {
                        result = valueTaskType.GetGenericArguments()[0];
                    }
                    else
                    {
                        result = null;
                    }

                    return result;
                }
            }
        }
        
    }

    /// <summary>
    /// Extends <see cref="Task{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The type of task result.</typeparam>
    extension<T>(Task<T>)
    {
        /// <summary>
        /// Gets delegate representing getter of <see cref="Task{T}.Result"/> property.
        /// </summary>
        /// <value>The delegate representing <see cref="Task{T}.Result"/> property getter.</value>
        public static Func<Task<T>, T> ResultGetter => Cache<T>.ResultGetter;
    }

    /// <summary>
    /// Extends <see cref="Task"/> type.
    /// </summary>
    /// <param name="task">The task to reflect.</param>
    extension(Task task)
    {
        /// <summary>
        /// Gets a delegate that representing getter method of <see cref="Task.IsCompleted"/> property
        /// captured for the specified task.
        /// </summary>
        /// <value>The delegate representing <see cref="Task.IsCompleted"/> property of <paramref name="task"/>.</value>
        public Func<bool> IsCompletedGetter
        {
            get
            {
                Push(task);
                Ldftn(PropertyGet(Type<Task>(), nameof(Task.IsCompleted)));
                Newobj(Constructor(Type<Func<bool>>(), Type<object>(), Type<IntPtr>()));
                return Return<Func<bool>>();
            }
        }

        /// <summary>
        /// Gets a delegate representing getter method of <see cref="Task.IsCompletedSuccessfully"/> property.
        /// </summary>
        public static Func<Task, bool> IsCompletedSuccessfullyGetter => CompletedSuccessfullyGetter;
    }
}