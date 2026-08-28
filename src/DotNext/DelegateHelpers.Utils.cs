using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Runtime.CompilerServices;

public static partial class DelegateHelpers
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct TargetRewriter(object target) : ISupplier<Delegate, object?>
    {
        object ISupplier<Delegate, object?>.Invoke(Delegate d) => target;
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

    private static TResult UnboxAny<T, TResult>(this object obj, T arg)
        where T : allows ref struct
        => obj.UnboxAny<TResult>();

    private sealed unsafe partial class MethodPointer
    {
        private readonly nuint pointer;

        private MethodPointer(void* pointer) => this.pointer = new(pointer);

        public override string ToString() => new nuint(pointer).ToString("X");

        public override bool Equals([NotNullWhen(true)] object? other)
            => other is MethodPointer methodPtr && methodPtr.pointer == pointer;

        public override int GetHashCode() => pointer.GetHashCode();
    }

    private sealed unsafe partial class MethodPointer<TTarget>
        where TTarget : class?
    {
        private readonly TTarget target;
        private readonly nuint pointer;

        private MethodPointer(void* pointer, TTarget target)
        {
            this.pointer = new(pointer);
            this.target = target;
        }
        
        public override string ToString() => pointer.ToString("X");

        public override bool Equals([NotNullWhen(true)] object? other)
            => other is MethodPointer<TTarget> methodPtr
               && methodPtr.pointer == pointer
               && ReferenceEquals(target, methodPtr.target);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(pointer);
            hash.Add(RuntimeHelpers.GetHashCode(target));
            return hash.ToHashCode();
        }
    }
}
