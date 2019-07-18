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
    /// <typeparam name="D">The type of the delegate that is compatible with the pointer.</typeparam>
    public interface IMethodPointer<out D>
        where D : Delegate
    {
        /// <summary>
        /// Converts function pointer into delegate.
        /// </summary>
        /// <returns>The delegate instance created from this pointer.</returns>
        D ToDelegate();

        /// <summary>
        /// Gets address of the function.
        /// </summary>
        IntPtr Address { get; }
    }

    internal sealed class MethodPointerException : NullReferenceException
    {
        internal MethodPointerException()
            : base(ExceptionMessages.NullMethodPointer)
        {
        }
    }

    /// <summary>
    /// Represents a pointer to parameterless method with return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<R> : IMethodPointer<Func<R>>, IEquatable<FunctionPointer<R>>
    {
        private readonly IntPtr methodPtr;

        public FunctionPointer(MethodInfo method)
        {
            if (!method.IsStatic || method.GetParameters().LongLength > 0L)
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
            methodPtr = method.MethodHandle.GetFunctionPointer();
        }

        public FunctionPointer(Func<R> @delegate)
            : this(@delegate.Method)
        {
        }

        IntPtr IMethodPointer<Func<R>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this function pointer.</returns>
        public Func<R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<R>), ".ctor", typeof(object), typeof(IntPtr)));
            return Return<Func<R>>();
        }

        /// <summary>
        /// Invokes function by pointer.
        /// </summary>
        /// <returns>The result of function invocation.</returns>
        public R Invoke()
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this function pointer.</returns>
        public static explicit operator Func<R>(FunctionPointer<R> pointer) => pointer.ToDelegate();

        public override int GetHashCode() => methodPtr.GetHashCode();

        public bool Equals(FunctionPointer<R> other) => methodPtr == other.methodPtr;

        public override bool Equals(object other) => other is IMethodPointer<Func<R>> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        public static bool operator ==(FunctionPointer<R> first, FunctionPointer<R> second)
            => first.methodPtr == second.methodPtr;

        public static bool operator !=(FunctionPointer<R> first, FunctionPointer<R> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and return type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    public readonly struct FunctionPointer<T, R> : IMethodPointer<Func<T, R>>
    {
        private readonly IntPtr methodPtr;

        public FunctionPointer(MethodInfo method)
        {
            var paramCount = method.GetParameters().LongLength;
            if (method.IsStatic ? paramCount != 1 : paramCount != 0)
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
            methodPtr = method.MethodHandle.GetFunctionPointer();
        }

        public FunctionPointer(Func<T, R> @delegate)
            : this(@delegate.Method)
        {
        }

        IntPtr IMethodPointer<Func<T, R>>.Address => methodPtr;

        public Func<T, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<T, R>), ".ctor", typeof(object), typeof(IntPtr)));
            return Return<Func<T, R>>();
        }

        public R Invoke(T arg)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T)));
            return Return<R>();
        }
    }
}
