using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;
using M = InlineIL.MethodRef;
using TR = InlineIL.TypeRef;

namespace DotNext
{
    internal sealed class MethodPointerException : NullReferenceException
    {
        internal MethodPointerException()
            : base(ExceptionMessages.NullMethodPointer)
        {
        }
    }

    /// <summary>
    /// Represents a pointer to parameterless method with <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <seealso cref="Reflection.MethodCookie{D}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D}"/>
    public readonly struct ActionPointer : IMethodPointer<Action>, IEquatable<ActionPointer>
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ActionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal ActionPointer(RuntimeMethodHandle method, object target)
        {
            this.target = target;
            methodPtr = method.GetFunctionPointer();
        }

        IntPtr IMethodPointer<Action>.Address => methodPtr;

        object IMethodPointer<Action>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action), typeof(object), typeof(IntPtr)));
            return Return<Action>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        public void Invoke()
        {
            const string callIndirect = "indirect";
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            Pop();
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void)));
            Ret();
            MarkLabel(callImplicitThis);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(ActionPointer first, ActionPointer second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer first, ActionPointer second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to parameterless method with return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<R> : IMethodPointer<Func<R>>, IEquatable<FunctionPointer<R>>, ISupplier<R>
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public FunctionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public FunctionPointer(Func<R> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal FunctionPointer(RuntimeMethodHandle method, object target)
            : this(method.GetFunctionPointer(), target)
        {
        }

        private FunctionPointer(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        private static R CreateDefault() => default(R);

        /// <summary>
        /// Returns activator for type <typeparamref name="R"/> as typed method pointer.
        /// </summary>
        /// <remarks>
        /// Actual type <typeparamref name="R"/> should be a value type or have public parameterless constructor. 
        /// </remarks>
        /// <returns></returns>
        public static FunctionPointer<R> CreateActivator()
        {
            const string HandleRefType = "refType";
            Ldtoken(typeof(R));
            Call(new M(typeof(Type), nameof(Type.GetTypeFromHandle)));
            Call(M.PropertyGet(typeof(Type), nameof(Type.IsValueType)));
            Brfalse(HandleRefType);

            Ldftn(new M(typeof(FunctionPointer<R>), nameof(CreateDefault)));
            Ldnull();
            Newobj(M.Constructor(typeof(FunctionPointer<R>), typeof(IntPtr), typeof(object)));
            Ret();

            MarkLabel(HandleRefType);
            Ldftn(new M(typeof(Activator), nameof(Activator.CreateInstance), Array.Empty<TR>()).MakeGenericMethod(typeof(R)));
            Ldnull();
            Newobj(M.Constructor(typeof(FunctionPointer<R>), typeof(IntPtr), typeof(object)));
            return Return<FunctionPointer<R>>();
        }

        IntPtr IMethodPointer<Func<R>>.Address => methodPtr;

        object IMethodPointer<Func<R>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<R>), typeof(object), typeof(IntPtr)));
            return Return<Func<R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <returns>The result of method invocation.</returns>
        public R Invoke()
        {
            const string callIndirect = "indirect";
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            Pop();
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R)));
            Ret();
            MarkLabel(callImplicitThis);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R)));
            return Return<R>();
        }

        R ISupplier<R>.Supply() => Invoke();

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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(FunctionPointer<R> first, FunctionPointer<R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<R> first, FunctionPointer<R> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public FunctionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public FunctionPointer(Func<T, R> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal FunctionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Func<T, R>>.Address => methodPtr;
        object IMethodPointer<Func<T, R>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T, R>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(FunctionPointer<T, R> first, FunctionPointer<T, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T, R> first, FunctionPointer<T, R> second) => first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ActionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal ActionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Action<T>>.Address => methodPtr;
        object IMethodPointer<Action<T>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T>), typeof(object), typeof(IntPtr)));
            return Return<Action<T>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        public void Invoke(T arg)
        {
            const string callIndirect = "indirect";
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(ActionPointer<T> first, ActionPointer<T> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T> first, ActionPointer<T> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public FunctionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T1, T2, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public FunctionPointer(Func<T1, T2, R> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal FunctionPointer(RuntimeMethodHandle method, object target)
            : this(method.GetFunctionPointer(), target)
        {
        }

        internal FunctionPointer(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        IntPtr IMethodPointer<Func<T1, T2, R>>.Address => methodPtr;
        object IMethodPointer<Func<T1, T2, R>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T1, T2, R>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T1), typeof(T2)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T1, T2, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(FunctionPointer<T1, T2, R> first, FunctionPointer<T1, T2, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T1, T2, R> first, FunctionPointer<T1, T2, R> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ActionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T1, T2>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T1, T2> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal ActionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Action<T1, T2>>.Address => methodPtr;
        object IMethodPointer<Action<T1, T2>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T1, T2>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T1), typeof(T2)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T1, T2> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(ActionPointer<T1, T2> first, ActionPointer<T1, T2> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T1, T2> first, ActionPointer<T1, T2> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public FunctionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T1, T2, T3, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public FunctionPointer(Func<T1, T2, T3, R> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal FunctionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Func<T1, T2, T3, R>>.Address => methodPtr;
        object IMethodPointer<Func<T1, T2, T3, R>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, R>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T1), typeof(T2), typeof(T3)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T1, T2, T3, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(FunctionPointer<T1, T2, T3, R> first, FunctionPointer<T1, T2, T3, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T1, T2, T3, R> first, FunctionPointer<T1, T2, T3, R> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ActionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T1, T2, T3>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T1, T2, T3> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal ActionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Action<T1, T2, T3>>.Address => methodPtr;
        object IMethodPointer<Action<T1, T2, T3>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T1), typeof(T2), typeof(T3)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T1, T2, T3> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(ActionPointer<T1, T2, T3> first, ActionPointer<T1, T2, T3> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T1, T2, T3> first, ActionPointer<T1, T2, T3> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public FunctionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T1, T2, T3, T4, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public FunctionPointer(Func<T1, T2, T3, T4, R> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal FunctionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Func<T1, T2, T3, T4, R>>.Address => methodPtr;
        object IMethodPointer<Func<T1, T2, T3, T4, R>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, T4, R>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T1, T2, T3, T4, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(FunctionPointer<T1, T2, T3, T4, R> first, FunctionPointer<T1, T2, T3, T4, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T1, T2, T3, T4, R> first, FunctionPointer<T1, T2, T3, T4, R> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ActionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T1, T2, T3, T4>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T1, T2, T3, T4> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal ActionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Action<T1, T2, T3, T4>>.Address => methodPtr;
        object IMethodPointer<Action<T1, T2, T3, T4>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3, T4>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T1, T2, T3, T4> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(ActionPointer<T1, T2, T3, T4> first, ActionPointer<T1, T2, T3, T4> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T1, T2, T3, T4> first, ActionPointer<T1, T2, T3, T4> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public FunctionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T1, T2, T3, T4, T5, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public FunctionPointer(Func<T1, T2, T3, T4, T5, R> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal FunctionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Func<T1, T2, T3, T4, T5, R>>.Address => methodPtr;
        object IMethodPointer<Func<T1, T2, T3, T4, T5, R>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, T4, T5, R>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<T1, T2, T3, T4, T5, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(FunctionPointer<T1, T2, T3, T4, T5, R> first, FunctionPointer<T1, T2, T3, T4, T5, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<T1, T2, T3, T4, T5, R> first, FunctionPointer<T1, T2, T3, T4, T5, R> second) => !first.Equals(second);
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
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D}"/> or <see cref="Reflection.MethodCookie{T,D}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ActionPointer(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T1, T2, T3, T4, T5>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T1, T2, T3, T4, T5> @delegate)
        {
            methodPtr = @delegate.Method.MethodHandle.GetFunctionPointer();
            target = @delegate.Target;
        }

        internal ActionPointer(RuntimeMethodHandle method, object target)
        {
            methodPtr = method.GetFunctionPointer();
            this.target = target;
        }

        IntPtr IMethodPointer<Action<T1, T2, T3, T4, T5>>.Address => methodPtr;
        object IMethodPointer<Action<T1, T2, T3, T4, T5>>.Target => target;

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
            Push(target);
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3, T4, T5>), typeof(object), typeof(IntPtr)));
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
            const string callImplicitThis = "implicitThis";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(M.Constructor(typeof(MethodPointerException)));
            Throw();
            MarkLabel(callIndirect);
            
            Push(target);
            Dup();
            Brtrue(callImplicitThis);
            
            Pop();
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();
            
            MarkLabel(callImplicitThis);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
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
        public override int GetHashCode() => methodPtr.GetHashCode() ^ RuntimeHelpers.GetHashCode(target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T1, T2, T3, T4, T5> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address && ReferenceEquals(target, ptr.Target);

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
        public static bool operator ==(ActionPointer<T1, T2, T3, T4, T5> first, ActionPointer<T1, T2, T3, T4, T5> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T1, T2, T3, T4, T5> first, ActionPointer<T1, T2, T3, T4, T5> second) => !first.Equals(second);
    }
}
