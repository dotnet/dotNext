using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;
using M = InlineIL.MethodRef;

namespace DotNext
{
    using static Reflection.TypeExtensions;
    using static Reflection.DelegateType;

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
    /// Represents a source of method pointers.
    /// </summary>
    /// <remarks>
    /// The reason to having this method is to avoid heap allocations every time
    /// when you need typed method pointer. The constructor of such pointer
    /// performs runtime checks using Reflection. These checks require such allocations.
    /// To avoid that, it is possible to create <see cref="MethodPointerSource{D, P}"/>
    /// once and store it in <c>static readonly</c> field. After that, every creation
    /// of typed pointer via <see cref="Pointer"/> doesn't produce unecessary memory allocations.
    /// </remarks>
    /// <typeparam name="D">The type of the delegate compatible with managed pointer.</typeparam>
    /// <typeparam name="P">The type of the managed pointer.</typeparam>
    public readonly struct MethodPointerSource<D, P>
        where D : Delegate
        where P : struct, IMethodPointer<D>
    {
        //TODO: Should be rewritten in C# 8 as follows: replace P struct constraint with unmanaged because earlier versions of C# don't support unmanaged constructed types
        private readonly RuntimeMethodHandle method;

        /// <summary>
        /// Initializes a new source of pointers for the method described by the passed delegate instance.
        /// </summary>
        /// <param name="delegate">The delegate referencing a method for which pointers should be created.</param>
        public MethodPointerSource(D @delegate)
        {
            method = Unsafe.SizeOf<P>() == IntPtr.Size && @delegate.Target is null && @delegate.Method.CheckMethodPointerSignature<D>() ?
                @delegate.Method.MethodHandle :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        /// <summary>
        /// Obtains pointer to the static method.
        /// </summary>
        /// <remarks>
        /// This property throws exception if this object was created with default constructor instead
        /// of <see cref="MethodPointerSource(D)"/>.
        /// </remarks>
        public P Pointer
        {
            get
            {
                var pointer = method.GetFunctionPointer();
                return Unsafe.As<IntPtr, P>(ref pointer);
            }
        }
    }

    /// <summary>
    /// Represents a pointer to parameterless method with <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <seealso cref="MethodPointerSource{D, P}"/>
    public readonly struct ActionPointer : IMethodPointer<Action>, IEquatable<ActionPointer>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="MethodPointerSource{D, P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public ActionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Action>() ? 
                method.MethodHandle.GetFunctionPointer():
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        /// <exception cref="ArgumentException"><see cref="Delegate.Target"/> is not <see langword="null"/>.</exception>
        public ActionPointer(Action @delegate)
        {
            methodPtr = @delegate.Target is null ? 
                @delegate.Method.MethodHandle.GetFunctionPointer() : 
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Action>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Action), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Action>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        public void Invoke()
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action(ActionPointer pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ActionPointer first, ActionPointer second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer first, ActionPointer second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to parameterless method with return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<R> : IMethodPointer<Func<R>>, IEquatable<FunctionPointer<R>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="MethodPointerSource{D, P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public FunctionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Func<R>>() ? 
                    method.MethodHandle.GetFunctionPointer() :
                    throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        /// <exception cref="ArgumentException"><see cref="Delegate.Target"/> is not <see langword="null"/>.</exception>
        public FunctionPointer(Func<R> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate)); 
        }

        IntPtr IMethodPointer<Func<R>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
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
            Newobj(new M(typeof(Func<R>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Func<R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <returns>The result of method invocation.</returns>
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
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<R>(FunctionPointer<R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<R> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(FunctionPointer<R> first, FunctionPointer<R> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<R> first, FunctionPointer<R> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<T, R> : IMethodPointer<Func<T, R>>, IEquatable<FunctionPointer<T, R>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="MethodPointerSource{D, P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public FunctionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Func<T, R>>() ?
                method.MethodHandle.GetFunctionPointer():
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        /// <exception cref="ArgumentException"><see cref="Delegate.Target"/> is not <see langword="null"/>.</exception>
        public FunctionPointer(Func<T, R> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Func<T, R>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
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
            Newobj(new M(typeof(Func<T, R>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Func<T, R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
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

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T, R>(FunctionPointer<T, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T, R> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(FunctionPointer<T, R> first, FunctionPointer<T, R> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T, R> first, FunctionPointer<T, R> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    public readonly struct ActionPointer<T> : IMethodPointer<Action<T>>, IEquatable<ActionPointer<T>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <param name="method">The method to convert into pointer.</param>
        public ActionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Action<T>>() ? 
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Action<T>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Action<T>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Action<T>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        public void Invoke(T arg)
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
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T>(ActionPointer<T> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ActionPointer<T> first, ActionPointer<T> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T> first, ActionPointer<T> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with two parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<T1, T2, R> : IMethodPointer<Func<T1, T2, R>>, IEquatable<FunctionPointer<T1, T2, R>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="MethodPointerSource{D, P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public FunctionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Func<T1, T2, R>>() ?
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        /// <exception cref="ArgumentException"><see cref="Delegate.Target"/> is not <see langword="null"/>.</exception>
        public FunctionPointer(Func<T1, T2, R> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Func<T1, T2, R>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<T1, T2, R>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Func<T1, T2, R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public R Invoke(T1 arg1, T2 arg2)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, R>(FunctionPointer<T1, T2, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T1, T2, R> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(FunctionPointer<T1, T2, R> first, FunctionPointer<T1, T2, R> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T1, T2, R> first, FunctionPointer<T1, T2, R> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with two parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    public readonly struct ActionPointer<T1, T2> : IMethodPointer<Action<T1, T2>>, IEquatable<ActionPointer<T1, T2>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <param name="method">The method to convert into pointer.</param>
        public ActionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Action<T1, T2>>() ?
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T1, T2> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Action<T1, T2>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Action<T1, T2>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Action<T1, T2>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        public void Invoke(T1 arg1, T2 arg2)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2>(ActionPointer<T1, T2> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T1, T2> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ActionPointer<T1, T2> first, ActionPointer<T1, T2> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T1, T2> first, ActionPointer<T1, T2> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with three parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<T1, T2, T3, R> : IMethodPointer<Func<T1, T2, T3, R>>, IEquatable<FunctionPointer<T1, T2, T3, R>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="MethodPointerSource{D, P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public FunctionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Func<T1, T2, T3, R>>() ?
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        /// <exception cref="ArgumentException"><see cref="Delegate.Target"/> is not <see langword="null"/>.</exception>
        public FunctionPointer(Func<T1, T2, T3, R> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Func<T1, T2, T3, R>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<T1, T2, T3, R>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Func<T1, T2, T3, R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public R Invoke(T1 arg1, T2 arg2, T3 arg3)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, R>(FunctionPointer<T1, T2, T3, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T1, T2, T3, R> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(FunctionPointer<T1, T2, T3, R> first, FunctionPointer<T1, T2, T3, R> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T1, T2, T3, R> first, FunctionPointer<T1, T2, T3, R> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with three parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    public readonly struct ActionPointer<T1, T2, T3> : IMethodPointer<Action<T1, T2, T3>>, IEquatable<ActionPointer<T1, T2, T3>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <param name="method">The method to convert into pointer.</param>
        public ActionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Action<T1, T2, T3>>() ?
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T1, T2, T3> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Action<T1, T2, T3>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Action<T1, T2, T3>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Action<T1, T2, T3>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        public void Invoke(T1 arg1, T2 arg2, T3 arg3)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3>(ActionPointer<T1, T2, T3> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T1, T2, T3> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ActionPointer<T1, T2, T3> first, ActionPointer<T1, T2, T3> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T1, T2, T3> first, ActionPointer<T1, T2, T3> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with four parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<T1, T2, T3, T4, R> : IMethodPointer<Func<T1, T2, T3, T4, R>>, IEquatable<FunctionPointer<T1, T2, T3, T4, R>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="MethodPointerSource{D, P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public FunctionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Func<T1, T2, T3, T4, R>>() ?
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        /// <exception cref="ArgumentException"><see cref="Delegate.Target"/> is not <see langword="null"/>.</exception>
        public FunctionPointer(Func<T1, T2, T3, T4, R> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Func<T1, T2, T3, T4, R>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<T1, T2, T3, T4, R>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Func<T1, T2, T3, T4, R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <param name="arg4">The fourth argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public R Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, T4, R>(FunctionPointer<T1, T2, T3, T4, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T1, T2, T3, T4, R> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(FunctionPointer<T1, T2, T3, T4, R> first, FunctionPointer<T1, T2, T3, T4, R> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T1, T2, T3, T4, R> first, FunctionPointer<T1, T2, T3, T4, R> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with fourth parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    public readonly struct ActionPointer<T1, T2, T3, T4> : IMethodPointer<Action<T1, T2, T3, T4>>, IEquatable<ActionPointer<T1, T2, T3, T4>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <param name="method">The method to convert into pointer.</param>
        public ActionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Action<T1, T2, T3, T4>>() ?
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T1, T2, T3, T4> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Action<T1, T2, T3, T4>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Action<T1, T2, T3, T4>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Action<T1, T2, T3, T4>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <param name="arg4">The fourth argument to be passed into the target method.</param>
        public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3, T4>(ActionPointer<T1, T2, T3, T4> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T1, T2, T3, T4> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ActionPointer<T1, T2, T3, T4> first, ActionPointer<T1, T2, T3, T4> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T1, T2, T3, T4> first, ActionPointer<T1, T2, T3, T4> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with five parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<T1, T2, T3, T4, T5, R> : IMethodPointer<Func<T1, T2, T3, T4, T5, R>>, IEquatable<FunctionPointer<T1, T2, T3, T4, T5, R>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="MethodPointerSource{D, P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public FunctionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Func<T1, T2, T3, T4, T5, R>>() ?
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        /// <exception cref="ArgumentException"><see cref="Delegate.Target"/> is not <see langword="null"/>.</exception>
        public FunctionPointer(Func<T1, T2, T3, T4, T5, R> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Func<T1, T2, T3, T4, T5, R>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, T5, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<T1, T2, T3, T4, T5, R>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Func<T1, T2, T3, T4, T5, R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <param name="arg4">The fourth argument to be passed into the target method.</param>
        /// <param name="arg5">The fifth argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public R Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, T4, T5, R>(FunctionPointer<T1, T2, T3, T4, T5, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T1, T2, T3, T4, T5, R> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(FunctionPointer<T1, T2, T3, T4, T5, R> first, FunctionPointer<T1, T2, T3, T4, T5, R> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T1, T2, T3, T4, T5, R> first, FunctionPointer<T1, T2, T3, T4, T5, R> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with fifth parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth method parameter.</typeparam>
    public readonly struct ActionPointer<T1, T2, T3, T4, T5> : IMethodPointer<Action<T1, T2, T3, T4, T5>>, IEquatable<ActionPointer<T1, T2, T3, T4, T5>>
    {
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <param name="method">The method to convert into pointer.</param>
        public ActionPointer(MethodInfo method)
        {
            methodPtr = method.CheckMethodPointerSignature<Action<T1, T2, T3, T4, T5>>() ?
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T1, T2, T3, T4, T5> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Action<T1, T2, T3, T4, T5>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4, T5> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Action<T1, T2, T3, T4, T5>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Action<T1, T2, T3, T4, T5>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <param name="arg4">The fourth argument to be passed into the target method.</param>
        /// <param name="arg5">The fifth argument to be passed into the target method.</param>
        public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3, T4, T5>(ActionPointer<T1, T2, T3, T4, T5> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T1, T2, T3, T4, T5> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ActionPointer<T1, T2, T3, T4, T5> first, ActionPointer<T1, T2, T3, T4, T5> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T1, T2, T3, T4, T5> first, ActionPointer<T1, T2, T3, T4, T5> second)
            => first.methodPtr != second.methodPtr;
    }
}
