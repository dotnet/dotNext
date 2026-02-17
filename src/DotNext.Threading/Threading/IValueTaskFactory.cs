namespace DotNext.Threading;

using Runtime;
using Runtime.CompilerServices;

internal interface IValueTaskFactory<T> : ISupplier<TimeSpan, CancellationToken, ValueTask>,
    ISupplier<TimeSpan, CancellationToken, ValueTask<T>>
{
    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(count, 2);

        var timeout = GetArgument<TimeSpan>(in args, 0);
        var token = GetArgument<CancellationToken>(in args, 1);
        if (result.TargetType == typeof(ValueTask))
        {
            result.Mutable<ValueTask>() = this.As<ISupplier<TimeSpan, CancellationToken, ValueTask>>().Invoke(timeout, token);
        }
        else
        {
            result.Mutable<ValueTask<T>>() = this.As<ISupplier<TimeSpan, CancellationToken, ValueTask<T>>>().Invoke(timeout, token);
        }
    }
}