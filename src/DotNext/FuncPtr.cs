using System;
using System.Reflection;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;
using M = InlineIL.MethodRef;

namespace DotNext
{
    using static Reflection.TypeExtensions;

    /// <summary>
    /// Represents common interface for static delegates.
    /// </summary>
    /// <typeparam name="D"></typeparam>
    public interface IMethodPointer<D>
        where D : Delegate
    {
        /// <summary>
        /// Converts function pointer into delegate.
        /// </summary>
        /// <returns></returns>
        D ToDelegate();

        /// <summary>
        /// Gets address of the function.
        /// </summary>
        IntPtr Address { get; }
    }

    public sealed class NullMethodException : NullReferenceException
    {

    }

    public readonly struct StaticFunc<R> : IMethodPointer<Func<R>>
    {
        private readonly IntPtr methodPtr;

        public StaticFunc(MethodInfo method)
        {
            if (!method.IsStatic)
                throw new ArgumentException("", nameof(method));
            if (method.GetParameters().LongLength > 0L)
                throw new ArgumentException("", nameof(method));
            methodPtr = method.MethodHandle.GetFunctionPointer();
        }

        public StaticFunc(Func<R> @delegate)
            : this(@delegate.Method)
        {
        }

        IntPtr IMethodPointer<Func<R>>.Address => methodPtr;

        public Func<R> ToDelegate()
        {
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<R>), ".ctor", typeof(object), typeof(IntPtr)));
            return Return<Func<R>>();
        }

        public R Invoke()
        {
            const string nullPtrDetected = "throw";
            Push(methodPtr);
            Brfalse(nullPtrDetected);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R)));
            Ret();
            MarkLabel(nullPtrDetected);
            Newobj(new M(typeof(NullMethodException), ".ctor"));
            Throw();
            throw Unreachable();
        }
    }

    public readonly struct StaticFunc<T, R> : IMethodPointer<Func<T, R>>
    {
        private readonly IntPtr methodPtr;

        public StaticFunc(MethodInfo method)
        {
            methodPtr = method.MethodHandle.GetFunctionPointer();
        }

        public StaticFunc(Func<T, R> @delegate)
            : this(@delegate.Method)
        {
        }

        IntPtr IMethodPointer<Func<T, R>>.Address => methodPtr;

        public Func<T, R> ToDelegate()
        {
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<R>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Func<T, R>>();
        }

        public R Invoke(T arg)
        {
            const string nullPtrDetected = "throw";
            Push(methodPtr);
            Brfalse(nullPtrDetected);
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T)));
            Ret();
            MarkLabel(nullPtrDetected);
            Newobj(new M(typeof(NullMethodException), ConstructorName));
            Throw();
            throw Unreachable();
        }
    }
}
