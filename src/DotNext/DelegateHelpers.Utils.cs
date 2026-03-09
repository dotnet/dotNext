using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Runtime.CompilerServices;

public static partial class DelegateHelpers
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct TargetRewriter(object target) : ISupplier<Delegate, object?>
    {
        object? ISupplier<Delegate, object?>.Invoke(Delegate d) => target;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct EmptyTargetRewriter : ISupplier<Delegate, object?>
    {
        object? ISupplier<Delegate, object?>.Invoke(Delegate d) => d.Target;
    }

    private static TDelegate ChangeType<TDelegate, TRewriter>(this Delegate d, TRewriter rewriter)
        where TDelegate : Delegate
        where TRewriter : struct, ISupplier<Delegate, object?>, allows ref struct
    {
        var delegateType = typeof(TDelegate);
        var enumerator = Delegate.EnumerateInvocationList(d);
        if (enumerator.MoveNext())
        {
            d = ChangeTypeCore(enumerator.Current, rewriter, delegateType);
            
            while (enumerator.MoveNext())
            {
                d = Delegate.Combine(d, ChangeTypeCore(enumerator.Current, rewriter, delegateType));
            }
        }

        return (TDelegate)d;
    }

    private static Delegate ChangeTypeCore<TRewriter>(Delegate d, TRewriter rewriter, Type delegateType)
        where TRewriter : struct, ISupplier<Delegate, object?>, allows ref struct
        => d.Method.CreateDelegate(delegateType, rewriter.Invoke(d));

    private static Func<bool> FromBoolConstant(bool value)
        => value ? True : False;

    private static Func<T, bool> FromBoolConstant<T>(bool value)
        where T : allows ref struct
        => value ? True : False;

    private static bool True() => true;
    
    private static bool True<T>(T value) where T : allows ref struct => true;

    private static bool False() => false;

    private static bool False<T>(T value) where T : allows ref struct => false;

    internal static T? Default<T>() where T : allows ref struct => default;

    private static TResult? Default<T, TResult>(T arg)
        where T : allows ref struct
        where TResult : allows ref struct
        => default;

    private static T UnboxAny<T>(this object obj)
        => typeof(T).IsValueType
            ? Unsafe.As<byte, T>(ref Unsafe.GetRawData(obj))
            : Unsafe.As<object, T>(ref obj);

    private static TResult UnboxAny<T, TResult>(this object obj, T arg)
        where T : allows ref struct
        => typeof(TResult).IsValueType
            ? Unsafe.As<byte, TResult>(ref Unsafe.GetRawData(obj))
            : Unsafe.As<object, TResult>(ref obj);
}