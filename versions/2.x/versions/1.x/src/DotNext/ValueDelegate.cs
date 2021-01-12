using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;
using M = InlineIL.MethodRef;
using TR = InlineIL.TypeRef;

namespace DotNext
{
    using Runtime.CompilerServices;
    using Intrinsics = Runtime.Intrinsics;

    /// <summary>
    /// Represents a pointer to parameterless method with <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueAction : ICallable<Action>, IEquatable<ValueAction>
    {
        private readonly IntPtr methodPtr;
        private readonly Action action;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method)
            : this(method.CreateDelegate<Action>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="action">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="action"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action action, bool wrap = false)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (wrap || DelegateHelpers.IsRegularDelegate(action))
            {
                this.action = action;
                methodPtr = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueAction([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            action = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(action);
            return Return<Action>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        public void Invoke()
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Dup();
            Brfalse(callDelegate);

            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void)));
            Ret();

            MarkLabel(callDelegate);
            Pop();
            Push(action);
            Callvirt(new M(typeof(Action), nameof(Invoke)));
            Ret();
        }

        object ICallable.DynamicInvoke(params object[] args)
        {
            Invoke();
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action(in ValueAction pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueAction action && Equals(action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction first, in ValueAction second) => first.methodPtr == second.methodPtr && Equals(first.action, second.action);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction first, in ValueAction second) => first.methodPtr != second.methodPtr || !Equals(first.action, second.action);
    }

    /// <summary>
    /// Represents a pointer to parameterless method with return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<R> : ICallable<Func<R>>, IEquatable<ValueFunc<R>>, ISupplier<R>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<R> func;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method)
            : this(method.CreateDelegate<Func<R>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="func">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="func"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<R> func, bool wrap = false)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (wrap || DelegateHelpers.IsRegularDelegate(func))
            {
                this.func = func;
                methodPtr = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueFunc([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            func = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == default;

        /// <summary>
        /// Returns activator for type <typeparamref name="R"/> in the form of typed method pointer.
        /// </summary>
        /// <remarks>
        /// Actual type <typeparamref name="R"/> should be a value type or have public parameterless constructor. 
        /// </remarks>
        public static ValueFunc<R> Activator
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Ldftn(new M(typeof(Activator), nameof(System.Activator.CreateInstance), Array.Empty<TR>()).MakeGenericMethod(typeof(R)));
                Newobj(M.Constructor(typeof(ValueFunc<R>), new TR(typeof(IntPtr)).WithRequiredModifier(typeof(ManagedMethodPointer))));
                return Return<ValueFunc<R>>();
            }
        }

        /// <summary>
        /// Obtains pointer to the method that returns <see langword="null"/> if <typeparamref name="R"/>
        /// is reference type or initialized value type if <typeparamref name="R"/> is value type.
        /// </summary>
        /// <value></value>
        public static ValueFunc<R> DefaultValueProvider
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Ldftn(new M(typeof(Intrinsics), nameof(Intrinsics.DefaultOf)).MakeGenericMethod(typeof(R)));
                Newobj(M.Constructor(typeof(ValueFunc<R>), new TR(typeof(IntPtr)).WithRequiredModifier(typeof(ManagedMethodPointer))));
                return Return<ValueFunc<R>>();
            }
        }

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<R> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Func<R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <returns>The result of method invocation.</returns>
        public R Invoke()
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Dup();
            Brfalse(callDelegate);

            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R)));
            Ret();

            MarkLabel(callDelegate);
            Pop();
            Push(func);
            Callvirt(new M(typeof(Func<R>), nameof(Invoke)));
            return Return<R>();
        }

        object ICallable.DynamicInvoke(params object[] args) => Invoke();

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<R>(in ValueFunc<R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueFunc<R> func && Equals(func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<R> first, in ValueFunc<R> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<R> first, in ValueFunc<R> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T, R> : ICallable<Func<T, R>>, ICallable<Converter<T, R>>, IEquatable<ValueFunc<T, R>>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T, R> func;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method)
            : this(method.CreateDelegate<Func<T, R>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="func">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="func"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T, R> func, bool wrap = false)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (wrap || DelegateHelpers.IsRegularDelegate(func))
            {
                this.func = func;
                methodPtr = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == default;

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueFunc([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            func = null;
            this.methodPtr = methodPtr;
        }

        private Converter<T, R> ToConverter()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Converter<T, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Converter<T, R>>();
        }

        Converter<T, R> ICallable<Converter<T, R>>.ToDelegate() => ToConverter();

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T, R> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Func<T, R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public R Invoke(T arg)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg);
            Callvirt(new M(typeof(Func<T, R>), nameof(Invoke)));
            return Return<R>();
        }

        object ICallable.DynamicInvoke(params object[] args) => Invoke((T)args[0]);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <param name="func">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T, R>(in ValueFunc<T, R> func) => func.ToDelegate();

        /// <summary>
        /// Converts this pointer into <see cref="Converter{T, TResult}"/>.
        /// </summary>
        /// <param name="func">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Converter<T, R>(in ValueFunc<T, R> func)
            => func.ToConverter();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueFunc<T, R> func && Equals(func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T, R> first, in ValueFunc<T, R> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T, R> first, in ValueFunc<T, R> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueAction<T> : ICallable<Action<T>>, IEquatable<ValueAction<T>>, IConsumer<T>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T> action;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method)
            : this(method.CreateDelegate<Action<T>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="action">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="action"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T> action, bool wrap = false)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (wrap || DelegateHelpers.IsRegularDelegate(action))
            {
                this.action = action;
                methodPtr = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueAction([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            action = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(action);
            return Return<Action<T>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        public void Invoke(T arg)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg);
            Callvirt(new M(typeof(Action<T>), nameof(Invoke)));
            Ret();
        }

        object ICallable.DynamicInvoke(params object[] args)
        {
            Invoke((T)args[0]);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T>(in ValueAction<T> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueAction<T> action && Equals(action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T> first, in ValueAction<T> second) => first.methodPtr == second.methodPtr && Equals(first.action, second.action);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T> first, in ValueAction<T> second) => first.methodPtr != second.methodPtr || !Equals(first.action, second.action);
    }

    /// <summary>
    /// Represents a pointer to the method with two parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T1, T2, R> : ICallable<Func<T1, T2, R>>, IEquatable<ValueFunc<T1, T2, R>>, ISupplier<T1, T2, R>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T1, T2, R> func;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method)
            : this(method.CreateDelegate<Func<T1, T2, R>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="func">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="func"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, R> func, bool wrap = false)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (wrap || DelegateHelpers.IsRegularDelegate(func))
            {
                this.func = func;
                methodPtr = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueFunc([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            func = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, R> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T1, T2, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
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
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Callvirt(new M(typeof(Func<T1, T2, R>), nameof(Invoke)));
            return Return<R>();
        }

        object ICallable.DynamicInvoke(params object[] args) => Invoke((T1)args[0], (T2)args[1]);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, R>(in ValueFunc<T1, T2, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T1, T2, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueFunc<T1, T2, R> func && Equals(func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T1, T2, R> first, in ValueFunc<T1, T2, R> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, R> first, in ValueFunc<T1, T2, R> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
    }

    /// <summary>
    /// Represents a pointer to the method with two parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueAction<T1, T2> : ICallable<Action<T1, T2>>, IEquatable<ValueAction<T1, T2>>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T1, T2> action;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method)
            : this(method.CreateDelegate<Action<T1, T2>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="action">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="action"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2> action, bool wrap = false)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (wrap || DelegateHelpers.IsRegularDelegate(action))
            {
                this.action = action;
                methodPtr = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueAction([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            action = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T1, T2>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(action);
            return Return<Action<T1, T2>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        public void Invoke(T1 arg1, T2 arg2)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Callvirt(new M(typeof(Action<T1, T2>), nameof(Invoke)));
            Ret();
        }

        object ICallable.DynamicInvoke(params object[] args)
        {
            Invoke((T1)args[0], (T2)args[1]);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2>(in ValueAction<T1, T2> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T1, T2> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueAction<T1, T2> action && Equals(action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T1, T2> first, in ValueAction<T1, T2> second) => first.methodPtr == second.methodPtr && Equals(first.action, second.action);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2> first, in ValueAction<T1, T2> second) => first.methodPtr != second.methodPtr || !Equals(first.action, second.action);
    }

    /// <summary>
    /// Represents a pointer to the method with three parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T1, T2, T3, R> : ICallable<Func<T1, T2, T3, R>>, IEquatable<ValueFunc<T1, T2, T3, R>>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T1, T2, T3, R> func;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method)
            : this(method.CreateDelegate<Func<T1, T2, T3, R>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="func">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="func"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, R> func, bool wrap = false)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (wrap || DelegateHelpers.IsRegularDelegate(func))
            {
                this.func = func;
                methodPtr = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueFunc([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            func = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, R> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
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
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Callvirt(new M(typeof(Func<T1, T2, T3, R>), nameof(Invoke)));
            return Return<R>();
        }

        object ICallable.DynamicInvoke(params object[] args) => Invoke((T1)args[0], (T2)args[1], (T3)args[2]);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, R>(in ValueFunc<T1, T2, T3, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T1, T2, T3, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueFunc<T1, T2, T3, R> func && Equals(func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T1, T2, T3, R> first, in ValueFunc<T1, T2, T3, R> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, R> first, in ValueFunc<T1, T2, T3, R> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
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
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueAction<T1, T2, T3> : ICallable<Action<T1, T2, T3>>, IEquatable<ValueAction<T1, T2, T3>>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T1, T2, T3> action;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method)
            : this(method.CreateDelegate<Action<T1, T2, T3>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="action">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="action"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3> action, bool wrap = false)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (wrap || DelegateHelpers.IsRegularDelegate(action))
            {
                this.action = action;
                methodPtr = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueAction([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            action = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2,T3}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(action);
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
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Callvirt(new M(typeof(Action<T1, T2, T3>), nameof(Invoke)));
            Ret();
        }

        object ICallable.DynamicInvoke(params object[] args)
        {
            Invoke((T1)args[0], (T2)args[1], (T3)args[2]);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3>(in ValueAction<T1, T2, T3> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T1, T2, T3> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueAction<T1, T2, T3> action && Equals(action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T1, T2, T3> first, in ValueAction<T1, T2, T3> second) => first.methodPtr == second.methodPtr && Equals(first.action, second.action);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3> first, in ValueAction<T1, T2, T3> second) => first.methodPtr != second.methodPtr || !Equals(first.action, second.action);
    }

    /// <summary>
    /// Represents a pointer to the method with four parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T1, T2, T3, T4, R> : ICallable<Func<T1, T2, T3, T4, R>>, IEquatable<ValueFunc<T1, T2, T3, T4, R>>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T1, T2, T3, T4, R> func;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method)
            : this(method.CreateDelegate<Func<T1, T2, T3, T4, R>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="func">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="func"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, T4, R> func, bool wrap = false)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (wrap || DelegateHelpers.IsRegularDelegate(func))
            {
                this.func = func;
                methodPtr = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueFunc([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            func = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, R> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, T4, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
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
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Callvirt(new M(typeof(Func<T1, T2, T3, T4, R>), nameof(Invoke)));
            return Return<R>();
        }

        object ICallable.DynamicInvoke(params object[] args) => Invoke((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, T4, R>(in ValueFunc<T1, T2, T3, T4, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T1, T2, T3, T4, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueFunc<T1, T2, T3, T4, R> func && Equals(func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T1, T2, T3, T4, R> first, in ValueFunc<T1, T2, T3, T4, R> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, T4, R> first, in ValueFunc<T1, T2, T3, T4, R> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
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
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueAction<T1, T2, T3, T4> : ICallable<Action<T1, T2, T3, T4>>, IEquatable<ValueAction<T1, T2, T3, T4>>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T1, T2, T3, T4> action;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method)
            : this(method.CreateDelegate<Action<T1, T2, T3, T4>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="action">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="action"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3, T4> action, bool wrap = false)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (wrap || DelegateHelpers.IsRegularDelegate(action))
            {
                this.action = action;
                methodPtr = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueAction([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            action = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2,T3,T4}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3, T4>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(action);
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
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Callvirt(new M(typeof(Action<T1, T2, T3, T4>), nameof(Invoke)));
            Ret();
        }

        object ICallable.DynamicInvoke(params object[] args)
        {
            Invoke((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3, T4>(in ValueAction<T1, T2, T3, T4> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T1, T2, T3, T4> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueAction<T1, T2, T3, T4> action && Equals(action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T1, T2, T3, T4> first, in ValueAction<T1, T2, T3, T4> second) => first.methodPtr == second.methodPtr && Equals(first.action, second.action);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3, T4> first, in ValueAction<T1, T2, T3, T4> second) => first.methodPtr != second.methodPtr || !Equals(first.action, second.action);
    }

    /// <summary>
    /// Represents a pointer to the method with five parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T1, T2, T3, T4, T5, R> : ICallable<Func<T1, T2, T3, T4, T5, R>>, IEquatable<ValueFunc<T1, T2, T3, T4, T5, R>>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T1, T2, T3, T4, T5, R> func;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method)
            : this(method.CreateDelegate<Func<T1, T2, T3, T4, T5, R>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="func">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="func"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, T4, T5, R> func, bool wrap = false)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (wrap || DelegateHelpers.IsRegularDelegate(func))
            {
                this.func = func;
                methodPtr = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueFunc([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            func = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, T5, R> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, T4, T5, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
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
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Callvirt(new M(typeof(Func<T1, T2, T3, T4, T5, R>), nameof(Invoke)));
            return Return<R>();
        }

        object ICallable.DynamicInvoke(params object[] args) => Invoke((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3], (T5)args[4]);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, T4, T5, R>(in ValueFunc<T1, T2, T3, T4, T5, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T1, T2, T3, T4, T5, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueFunc<T1, T2, T3, T4, T5, R> func && Equals(func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T1, T2, T3, T4, T5, R> first, in ValueFunc<T1, T2, T3, T4, T5, R> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, T4, T5, R> first, in ValueFunc<T1, T2, T3, T4, T5, R> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
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
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueAction<T1, T2, T3, T4, T5> : ICallable<Action<T1, T2, T3, T4, T5>>, IEquatable<ValueAction<T1, T2, T3, T4, T5>>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T1, T2, T3, T4, T5> action;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method)
            : this(method.CreateDelegate<Action<T1, T2, T3, T4, T5>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="action">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="action"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3, T4, T5> action, bool wrap = false)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (wrap || DelegateHelpers.IsRegularDelegate(action))
            {
                this.action = action;
                methodPtr = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueAction([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            action = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4, T5> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3, T4, T5>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(action);
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
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Callvirt(new M(typeof(Action<T1, T2, T3, T4, T5>), nameof(Invoke)));
            Ret();
        }

        object ICallable.DynamicInvoke(params object[] args)
        {
            Invoke((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3], (T5)args[4]);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3, T4, T5>(in ValueAction<T1, T2, T3, T4, T5> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T1, T2, T3, T4, T5> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueAction<T1, T2, T3, T4, T5> action && Equals(action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T1, T2, T3, T4, T5> first, in ValueAction<T1, T2, T3, T4, T5> second) => first.methodPtr == second.methodPtr && Equals(first.action, second.action);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3, T4, T5> first, in ValueAction<T1, T2, T3, T4, T5> second) => first.methodPtr != second.methodPtr || !Equals(first.action, second.action);
    }

    /// <summary>
    /// Represents action that accepts arbitrary value by reference.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the object to be passed by reference into the action.</typeparam>
    /// <typeparam name="TArgs">The type of the arguments to be passed into the action.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueRefAction<T, TArgs> : ICallable<RefAction<T, TArgs>>, IEquatable<ValueRefAction<T, TArgs>>
    {
        private readonly IntPtr methodPtr;
        private readonly RefAction<T, TArgs> action;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueRefAction(MethodInfo method)
            : this(method.CreateDelegate<RefAction<T, TArgs>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="action">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="action"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueRefAction(RefAction<T, TArgs> action, bool wrap = false)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (wrap || DelegateHelpers.IsRegularDelegate(action))
            {
                this.action = action;
                methodPtr = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueRefAction([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            action = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="RefAction{T, TArgs}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public RefAction<T, TArgs> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(RefAction<T, TArgs>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(action);
            return Return<RefAction<T, TArgs>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="reference">The object passed by reference.</param>
        /// <param name="args">The action arguments.</param>
        public void Invoke(ref T reference, TArgs args)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(ref reference);
            Push(args);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), new TR(typeof(T)).MakeByRefType(), typeof(TArgs)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(ref reference);
            Push(args);
            Callvirt(new M(typeof(RefAction<T, TArgs>), nameof(Invoke)));
            Ret();
        }

        object ICallable.DynamicInvoke(params object[] args)
        {
            var reference = (T)args[0];
            Invoke(ref reference, (TArgs)args[1]);
            args[0] = reference;
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="RefAction{T, TArgs}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator RefAction<T, TArgs>(in ValueRefAction<T, TArgs> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueRefAction<T, TArgs> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueRefAction<T, TArgs> action && Equals(action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueRefAction<T, TArgs> first, in ValueRefAction<T, TArgs> second) => first.methodPtr == second.methodPtr && Equals(first.action, second.action);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueRefAction<T, TArgs> first, in ValueRefAction<T, TArgs> second) => first.methodPtr != second.methodPtr || !Equals(first.action, second.action);
    }

    /// <summary>
    /// Represents function that accepts arbitrary value by reference.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the object to be passed by reference into the action.</typeparam>
    /// <typeparam name="TArgs">The type of the arguments to be passed into the action.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that this delegate encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueRefFunc<T, TArgs, TResult> : ICallable<RefFunc<T, TArgs, TResult>>, IEquatable<ValueRefFunc<T, TArgs, TResult>>
    {
        private readonly IntPtr methodPtr;
        private readonly RefFunc<T, TArgs, TResult> func;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the delegate type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueRefFunc(MethodInfo method)
            : this(method.CreateDelegate<RefFunc<T, TArgs, TResult>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="func">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="func"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueRefFunc(RefFunc<T, TArgs, TResult> func, bool wrap = false)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (wrap || DelegateHelpers.IsRegularDelegate(func))
            {
                this.func = func;
                methodPtr = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        /// <summary>
        /// Initializes a new delegate using pointer to the static managed method.
        /// </summary>
        /// <param name="methodPtr">The pointer to the static managed method.</param>
        [RuntimeFeatures(Augmentation = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public ValueRefFunc([RequiredModifier(typeof(ManagedMethodPointer))] IntPtr methodPtr)
        {
            func = null;
            this.methodPtr = methodPtr;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == default;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="RefFunc{T, TArgs, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public RefFunc<T, TArgs, TResult> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(RefFunc<T, TArgs, TResult>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<RefFunc<T, TArgs, TResult>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="reference">The object passed by reference.</param>
        /// <param name="args">The action arguments.</param>
        public TResult Invoke(ref T reference, TArgs args)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(ref reference);
            Push(args);
            Push(methodPtr);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(TResult), new TR(typeof(T)).MakeByRefType(), typeof(TArgs)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(ref reference);
            Push(args);
            Callvirt(new M(typeof(RefFunc<T, TArgs, TResult>), nameof(Invoke)));
            return Return<TResult>();
        }

        object ICallable.DynamicInvoke(params object[] args)
        {
            var reference = (T)args[0];
            var result = Invoke(ref reference, (TArgs)args[1]);
            args[0] = reference;
            return result;
        }

        /// <summary>
        /// Converts this pointer into <see cref="RefFunc{T, TArgs, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator RefFunc<T, TArgs, TResult>(in ValueRefFunc<T, TArgs, TResult> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func?.GetHashCode() ?? methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueRefFunc<T, TArgs, TResult> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueRefFunc<T, TArgs, TResult> func && Equals(func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueRefFunc<T, TArgs, TResult> first, in ValueRefFunc<T, TArgs, TResult> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueRefFunc<T, TArgs, TResult> first, in ValueRefFunc<T, TArgs, TResult> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
    }
}
