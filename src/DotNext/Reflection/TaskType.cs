using System.Diagnostics.CodeAnalysis;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Reflection;

using Runtime.CompilerServices;

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
            Ldnull();
            Ldftn(PropertyGet(Type<Task<T>>(), nameof(Task<T>.Result)));
            Newobj(Constructor(Type<Func<Task<T>, T>>(), Type<object>(), Type<IntPtr>()));
            Pop(out Func<Task<T>, T> propertyGetter);
            ResultGetter = propertyGetter;
        }
    }

    internal static readonly Type CompletedTaskType;

    static TaskType()
    {
        CompletedTaskType = Task.CompletedTask.GetType();

        Ldnull();
        Ldftn(PropertyGet(Type<Task>(), nameof(Task.IsCompletedSuccessfully)));
        Newobj(Constructor(Type<Func<Task, bool>>(), Type<object>(), Type<IntPtr>()));
        Pop(out Func<Task, bool> propertyGetter);
        IsCompletedSuccessfullyGetter = propertyGetter;
    }

    /// <summary>
    /// Returns task type for the specified result type.
    /// </summary>
    /// <param name="taskResult">Task result type.</param>
    /// <returns>Returns <see cref="Task"/> if <paramref name="taskResult"/> is <see cref="Void"/>; or <see cref="Task{TResult}"/> with actual generic argument equals to <paramref name="taskResult"/>.</returns>
    /// <seealso cref="Task"/>
    /// <seealso cref="Task{TResult}"/>
    [RequiresUnreferencedCode("Runtime binding may be incompatible with IL trimming")]
    public static Type MakeTaskType(this Type taskResult)
        => MakeTaskType(taskResult, valueTask: false);

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
    [RequiresUnreferencedCode("Runtime generic instantiation may be incompatible with IL trimming")]
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

        // this is workaround for .NET 5 and later
        // because Task.CompletedTask returning instance of generic type Task<TaskVoidResult>
        if (taskType == CompletedTaskType)
            return typeof(void);

        var result = taskType.FindGenericInstance(typeof(Task<>));
        if (result is not null)
            return result.GetGenericArguments()[0];

        if (typeof(Task).IsAssignableFrom(taskType))
            return typeof(void);

        return null;
    }

    /// <summary>
    /// Gets delegate representing getter of <see cref="Task{T}.Result"/> property.
    /// </summary>
    /// <typeparam name="T">The type of task result.</typeparam>
    /// <returns>The delegate representing <see cref="Task{T}.Result"/> property getter.</returns>
    public static Func<Task<T>, T> GetResultGetter<T>() => Cache<T>.ResultGetter;

    /// <summary>
    /// Gets delegate representing getter of <see cref="Task.IsCompletedSuccessfully"/> property.
    /// </summary>
    public static Func<Task, bool> IsCompletedSuccessfullyGetter { get; }
}