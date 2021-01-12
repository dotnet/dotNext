using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.StandAloneMethodSig;
using static InlineIL.TypeRef;

namespace DotNext
{
    using Runtime.CompilerServices;
    using Intrinsics = Runtime.Intrinsics;

    internal interface IValueDelegate<TDelegate> : ICallable<TDelegate>, ISupplier<TDelegate?>
        where TDelegate : Delegate
    {
        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        bool IsEmpty { get; }

        TDelegate? ISupplier<TDelegate?>.Invoke() => ToDelegate();
    }

    /// <summary>
    /// Represents a pointer to parameterless method with <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueAction : IValueDelegate<Action>, IEquatable<ValueAction>
    {
        private readonly IntPtr methodPtr;
        private readonly Action? action;

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
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Action>(), Type<object>(), Type<IntPtr>()));
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

            Calli(ManagedMethod(CallingConventions.Standard, typeof(void)));
            Ret();

            MarkLabel(callDelegate);
            Pop();
            Push(action);
            Callvirt(Method(Type<Action>(), nameof(Invoke)));
            Ret();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
        {
            Invoke();
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action?(in ValueAction pointer) => pointer.ToDelegate();

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
        public override bool Equals(object? other) => other is ValueAction action && Equals(action);

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
    /// <typeparam name="TResult">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<TResult> : IValueDelegate<Func<TResult>>, IEquatable<ValueFunc<TResult>>, ISupplier<TResult>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<TResult>? func;

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
            : this(method.CreateDelegate<Func<TResult>>())
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
        public ValueFunc(Func<TResult> func, bool wrap = false)
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
        /// Returns activator for type <typeparamref name="TResult"/> in the form of typed method pointer.
        /// </summary>
        /// <remarks>
        /// Actual type <typeparamref name="TResult"/> should be a value type or have public parameterless constructor.
        /// </remarks>
        public static ValueFunc<TResult> Activator
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Ldftn(Method(typeof(Activator), nameof(System.Activator.CreateInstance), 1).MakeGenericMethod(Type<TResult>()));
                Newobj(Constructor(Type<ValueFunc<TResult>>(), Type<IntPtr>().WithRequiredModifier(Type<ManagedMethodPointer>())));
                return Return<ValueFunc<TResult>>();
            }
        }

        /// <summary>
        /// Obtains pointer to the method that returns <see langword="null"/> if <typeparamref name="TResult"/>
        /// is reference type or initialized value type if <typeparamref name="TResult"/> is value type.
        /// </summary>
        public static ValueFunc<TResult> DefaultValueProvider
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Ldftn(Method(typeof(Intrinsics), nameof(Intrinsics.DefaultOf)).MakeGenericMethod(Type<TResult>()));
                Newobj(Constructor(Type<ValueFunc<TResult>>(), Type<IntPtr>().WithRequiredModifier(Type<ManagedMethodPointer>())));
                return Return<ValueFunc<TResult>>();
            }
        }

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<TResult>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Func<TResult>>(), Type<object>(), Type<IntPtr>()));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Func<TResult>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke()
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Dup();
            Brfalse(callDelegate);

            Calli(ManagedMethod(CallingConventions.Standard, Type<TResult>()));
            Ret();

            MarkLabel(callDelegate);
            Pop();
            Push(func);
            Callvirt(Method(Type<Func<TResult>>(), nameof(Invoke)));
            return Return<TResult>();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args) => Invoke();

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<TResult>?(in ValueFunc<TResult> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<TResult> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<TResult> func && Equals(func);

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
        public static bool operator ==(in ValueFunc<TResult> first, in ValueFunc<TResult> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<TResult> first, in ValueFunc<TResult> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T, TResult> : IValueDelegate<Func<T, TResult>>, IValueDelegate<Converter<T, TResult>>, IEquatable<ValueFunc<T, TResult>>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T, TResult>? func;

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
            : this(method.CreateDelegate<Func<T, TResult>>())
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
        public ValueFunc(Func<T, TResult> func, bool wrap = false)
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

        private Converter<T, TResult>? ToConverter()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Converter<T, TResult>>(), Type<object>(), Type<IntPtr>()));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Converter<T, TResult>>();
        }

        /// <inheritdoc/>
        Converter<T, TResult>? ICallable<Converter<T, TResult>>.ToDelegate() => ToConverter();

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T, TResult>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Func<T, TResult>>(), Type<object>(), Type<IntPtr>()));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Func<T, TResult>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke(T arg)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg);
            Push(methodPtr);
            Calli(ManagedMethod(CallingConventions.Standard, Type<TResult>(), Type<T>()));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg);
            Callvirt(Method(Type<Func<T, TResult>>(), nameof(Invoke)));
            return Return<TResult>();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args) => Invoke(Intrinsics.NullAwareCast<T>(args[0])!);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <param name="func">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T, TResult>?(in ValueFunc<T, TResult> func) => func.ToDelegate();

        /// <summary>
        /// Converts this pointer into <see cref="Converter{T, TResult}"/>.
        /// </summary>
        /// <param name="func">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Converter<T, TResult>?(in ValueFunc<T, TResult> func)
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
        public bool Equals(ValueFunc<T, TResult> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T, TResult> func && Equals(func);

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
        public static bool operator ==(in ValueFunc<T, TResult> first, in ValueFunc<T, TResult> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T, TResult> first, in ValueFunc<T, TResult> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueAction<T> : IValueDelegate<Action<T>>, IEquatable<ValueAction<T>>, IConsumer<T>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T>? action;

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
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Action<T>>(), Type<object>(), Type<IntPtr>()));
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
            Calli(ManagedMethod(CallingConventions.Standard, typeof(void), Type<T>()));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg);
            Callvirt(Method(Type<Action<T>>(), nameof(Invoke)));
            Ret();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
        {
            Invoke(Intrinsics.NullAwareCast<T>(args[0])!);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T>?(in ValueAction<T> pointer) => pointer.ToDelegate();

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
        public override bool Equals(object? other) => other is ValueAction<T> action && Equals(action);

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
    /// <typeparam name="TResult">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T1, T2, TResult> : IValueDelegate<Func<T1, T2, TResult>>, IEquatable<ValueFunc<T1, T2, TResult>>, ISupplier<T1, T2, TResult>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T1, T2, TResult>? func;

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
            : this(method.CreateDelegate<Func<T1, T2, TResult>>())
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
        public ValueFunc(Func<T1, T2, TResult> func, bool wrap = false)
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
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, TResult>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Func<T1, T2, TResult>>(), Type<object>(), Type<IntPtr>()));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Func<T1, T2, TResult>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke(T1 arg1, T2 arg2)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Calli(ManagedMethod(CallingConventions.Standard, Type<TResult>(), Type<T1>(), Type<T2>()));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Callvirt(Method(Type<Func<T1, T2, TResult>>(), nameof(Invoke)));
            return Return<TResult>();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
            => Invoke(Intrinsics.NullAwareCast<T1>(args[0])!, Intrinsics.NullAwareCast<T2>(args[1])!);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, TResult>?(in ValueFunc<T1, T2, TResult> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<T1, T2, TResult> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T1, T2, TResult> func && Equals(func);

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
        public static bool operator ==(in ValueFunc<T1, T2, TResult> first, in ValueFunc<T1, T2, TResult> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, TResult> first, in ValueFunc<T1, T2, TResult> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
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
    public readonly struct ValueAction<T1, T2> : IValueDelegate<Action<T1, T2>>, IEquatable<ValueAction<T1, T2>>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T1, T2>? action;

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
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Action<T1, T2>>(), Type<object>(), Type<IntPtr>()));
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
            Calli(ManagedMethod(CallingConventions.Standard, typeof(void), Type<T1>(), Type<T2>()));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Callvirt(Method(Type<Action<T1, T2>>(), nameof(Invoke)));
            Ret();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
        {
            Invoke(Intrinsics.NullAwareCast<T1>(args[0])!, Intrinsics.NullAwareCast<T2>(args[1])!);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2>?(in ValueAction<T1, T2> pointer) => pointer.ToDelegate();

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
        public override bool Equals(object? other) => other is ValueAction<T1, T2> action && Equals(action);

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
    /// <typeparam name="TResult">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T1, T2, T3, TResult> : IValueDelegate<Func<T1, T2, T3, TResult>>, IEquatable<ValueFunc<T1, T2, T3, TResult>>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T1, T2, T3, TResult>? func;

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
            : this(method.CreateDelegate<Func<T1, T2, T3, TResult>>())
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
        public ValueFunc(Func<T1, T2, T3, TResult> func, bool wrap = false)
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
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, TResult>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Func<T1, T2, T3, TResult>>(), Type<object>(), Type<IntPtr>()));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Func<T1, T2, T3, TResult>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke(T1 arg1, T2 arg2, T3 arg3)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Calli(ManagedMethod(CallingConventions.Standard, Type<TResult>(), Type<T1>(), Type<T2>(), Type<T3>()));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Callvirt(Method(Type<Func<T1, T2, T3, TResult>>(), nameof(Invoke)));
            return Return<TResult>();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
            => Invoke(Intrinsics.NullAwareCast<T1>(args[0])!, Intrinsics.NullAwareCast<T2>(args[1])!, Intrinsics.NullAwareCast<T3>(args[2])!);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, TResult>?(in ValueFunc<T1, T2, T3, TResult> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<T1, T2, T3, TResult> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T1, T2, T3, TResult> func && Equals(func);

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
        public static bool operator ==(in ValueFunc<T1, T2, T3, TResult> first, in ValueFunc<T1, T2, T3, TResult> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, TResult> first, in ValueFunc<T1, T2, T3, TResult> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
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
    public readonly struct ValueAction<T1, T2, T3> : IValueDelegate<Action<T1, T2, T3>>, IEquatable<ValueAction<T1, T2, T3>>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T1, T2, T3>? action;

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
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2,T3}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Action<T1, T2, T3>>(), Type<object>(), Type<IntPtr>()));
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
            Calli(ManagedMethod(CallingConventions.Standard, typeof(void), Type<T1>(), Type<T2>(), Type<T3>()));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Callvirt(Method(Type<Action<T1, T2, T3>>(), nameof(Invoke)));
            Ret();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
        {
            Invoke(Intrinsics.NullAwareCast<T1>(args[0])!, Intrinsics.NullAwareCast<T2>(args[1])!, Intrinsics.NullAwareCast<T3>(args[2])!);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3>?(in ValueAction<T1, T2, T3> pointer) => pointer.ToDelegate();

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
        public override bool Equals(object? other) => other is ValueAction<T1, T2, T3> action && Equals(action);

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
    /// <typeparam name="TResult">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T1, T2, T3, T4, TResult> : IValueDelegate<Func<T1, T2, T3, T4, TResult>>, IEquatable<ValueFunc<T1, T2, T3, T4, TResult>>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T1, T2, T3, T4, TResult>? func;

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
            : this(method.CreateDelegate<Func<T1, T2, T3, T4, TResult>>())
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
        public ValueFunc(Func<T1, T2, T3, T4, TResult> func, bool wrap = false)
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
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, TResult>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Func<T1, T2, T3, T4, TResult>>(), Type<object>(), Type<IntPtr>()));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Func<T1, T2, T3, T4, TResult>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <param name="arg4">The fourth argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Calli(ManagedMethod(CallingConventions.Standard, Type<TResult>(), Type<T1>(), Type<T2>(), Type<T3>(), Type<T4>()));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Callvirt(Method(Type<Func<T1, T2, T3, T4, TResult>>(), nameof(Invoke)));
            return Return<TResult>();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
            => Invoke(Intrinsics.NullAwareCast<T1>(args[0])!, Intrinsics.NullAwareCast<T2>(args[1])!, Intrinsics.NullAwareCast<T3>(args[2])!, Intrinsics.NullAwareCast<T4>(args[3])!);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, T4, TResult>?(in ValueFunc<T1, T2, T3, T4, TResult> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<T1, T2, T3, T4, TResult> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T1, T2, T3, T4, TResult> func && Equals(func);

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
        public static bool operator ==(in ValueFunc<T1, T2, T3, T4, TResult> first, in ValueFunc<T1, T2, T3, T4, TResult> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, T4, TResult> first, in ValueFunc<T1, T2, T3, T4, TResult> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
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
    public readonly struct ValueAction<T1, T2, T3, T4> : IValueDelegate<Action<T1, T2, T3, T4>>, IEquatable<ValueAction<T1, T2, T3, T4>>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T1, T2, T3, T4>? action;

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
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2,T3,T4}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Action<T1, T2, T3, T4>>(), Type<object>(), Type<IntPtr>()));
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
            Calli(ManagedMethod(CallingConventions.Standard, typeof(void), Type<T1>(), Type<T2>(), Type<T3>(), Type<T4>()));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Callvirt(Method(Type<Action<T1, T2, T3, T4>>(), nameof(Invoke)));
            Ret();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
        {
            Invoke(Intrinsics.NullAwareCast<T1>(args[0])!, Intrinsics.NullAwareCast<T2>(args[1])!, Intrinsics.NullAwareCast<T3>(args[2])!, Intrinsics.NullAwareCast<T4>(args[3])!);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3, T4>?(in ValueAction<T1, T2, T3, T4> pointer) => pointer.ToDelegate();

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
        public override bool Equals(object? other) => other is ValueAction<T1, T2, T3, T4> action && Equals(action);

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
    /// <typeparam name="TResult">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueFunc<T1, T2, T3, T4, T5, TResult> : IValueDelegate<Func<T1, T2, T3, T4, T5, TResult>>, IEquatable<ValueFunc<T1, T2, T3, T4, T5, TResult>>
    {
        private readonly IntPtr methodPtr;
        private readonly Func<T1, T2, T3, T4, T5, TResult>? func;

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
            : this(method.CreateDelegate<Func<T1, T2, T3, T4, T5, TResult>>())
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="func">The delegate representing method.</param>
        /// <param name="wrap"><see langword="true"/> to wrap <paramref name="func"/> into this delegate; <see langword="false"/> to extract method pointer without holding reference to the passed delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, T4, T5, TResult> func, bool wrap = false)
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
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, T5, TResult>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Func<T1, T2, T3, T4, T5, TResult>>(), Type<object>(), Type<IntPtr>()));
            Ret();

            MarkLabel(returnDelegate);
            Push(func);
            return Return<Func<T1, T2, T3, T4, T5, TResult>>();
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
        public TResult Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
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
            Calli(ManagedMethod(CallingConventions.Standard, Type<TResult>(), Type<T1>(), Type<T2>(), Type<T3>(), Type<T4>(), Type<T5>()));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Callvirt(Method(Type<Func<T1, T2, T3, T4, T5, TResult>>(), nameof(Invoke)));
            return Return<TResult>();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
            => Invoke(Intrinsics.NullAwareCast<T1>(args[0])!, Intrinsics.NullAwareCast<T2>(args[1])!, Intrinsics.NullAwareCast<T3>(args[2])!, Intrinsics.NullAwareCast<T4>(args[3])!, Intrinsics.NullAwareCast<T5>(args[4])!);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, T4, T5, TResult>?(in ValueFunc<T1, T2, T3, T4, T5, TResult> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<T1, T2, T3, T4, T5, TResult> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T1, T2, T3, T4, T5, TResult> func && Equals(func);

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
        public static bool operator ==(in ValueFunc<T1, T2, T3, T4, T5, TResult> first, in ValueFunc<T1, T2, T3, T4, T5, TResult> second) => first.methodPtr == second.methodPtr && Equals(first.func, second.func);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, T4, T5, TResult> first, in ValueFunc<T1, T2, T3, T4, T5, TResult> second) => first.methodPtr != second.methodPtr || !Equals(first.func, second.func);
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
    public readonly struct ValueAction<T1, T2, T3, T4, T5> : IValueDelegate<Action<T1, T2, T3, T4, T5>>, IEquatable<ValueAction<T1, T2, T3, T4, T5>>
    {
        private readonly IntPtr methodPtr;
        private readonly Action<T1, T2, T3, T4, T5>? action;

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
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4, T5>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<Action<T1, T2, T3, T4, T5>>(), Type<object>(), Type<IntPtr>()));
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
            Calli(ManagedMethod(CallingConventions.Standard, typeof(void), Type<T1>(), Type<T2>(), Type<T3>(), Type<T4>(), Type<T5>()));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Callvirt(Method(Type<Action<T1, T2, T3, T4, T5>>(), nameof(Invoke)));
            Ret();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
        {
            Invoke(Intrinsics.NullAwareCast<T1>(args[0])!, Intrinsics.NullAwareCast<T2>(args[1])!, Intrinsics.NullAwareCast<T3>(args[2])!, Intrinsics.NullAwareCast<T4>(args[3])!, Intrinsics.NullAwareCast<T5>(args[4])!);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3, T4, T5>?(in ValueAction<T1, T2, T3, T4, T5> pointer) => pointer.ToDelegate();

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
        public override bool Equals(object? other) => other is ValueAction<T1, T2, T3, T4, T5> action && Equals(action);

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
    public readonly struct ValueRefAction<T, TArgs> : IValueDelegate<RefAction<T, TArgs>>, IEquatable<ValueRefAction<T, TArgs>>
    {
        private readonly IntPtr methodPtr;
        private readonly RefAction<T, TArgs>? action;

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
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="RefAction{T, TArgs}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public RefAction<T, TArgs>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<RefAction<T, TArgs>>(), Type<object>(), Type<IntPtr>()));
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
            Calli(ManagedMethod(CallingConventions.Standard, typeof(void), Type<T>().MakeByRefType(), Type<TArgs>()));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(ref reference);
            Push(args);
            Callvirt(Method(Type<RefAction<T, TArgs>>(), nameof(Invoke)));
            Ret();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
        {
            var reference = Intrinsics.NullAwareCast<T>(args[0]);
            Invoke(ref reference!, Intrinsics.NullAwareCast<TArgs>(args[1])!);
            args[0] = reference;
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="RefAction{T, TArgs}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator RefAction<T, TArgs>?(in ValueRefAction<T, TArgs> pointer) => pointer.ToDelegate();

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
        public override bool Equals(object? other) => other is ValueRefAction<T, TArgs> action && Equals(action);

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
    public readonly struct ValueRefFunc<T, TArgs, TResult> : IValueDelegate<RefFunc<T, TArgs, TResult>>, IEquatable<ValueRefFunc<T, TArgs, TResult>>
    {
        private readonly IntPtr methodPtr;
        private readonly RefFunc<T, TArgs, TResult>? func;

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
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="RefFunc{T, TArgs, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public RefFunc<T, TArgs, TResult>? ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(methodPtr);
            Brfalse(returnDelegate);

            Ldnull();
            Push(methodPtr);
            Newobj(Constructor(Type<RefFunc<T, TArgs, TResult>>(), Type<object>(), Type<IntPtr>()));
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
        /// <returns>The value returned by underlying method.</returns>
        public TResult Invoke(ref T reference, TArgs args)
        {
            const string callDelegate = "delegate";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(ref reference);
            Push(args);
            Push(methodPtr);
            Calli(ManagedMethod(CallingConventions.Standard, Type<TResult>(), Type<T>().MakeByRefType(), Type<TArgs>()));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(ref reference);
            Push(args);
            Callvirt(Method(Type<RefFunc<T, TArgs, TResult>>(), nameof(Invoke)));
            return Return<TResult>();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(params object?[] args)
        {
            var reference = Intrinsics.NullAwareCast<T>(args[0]);
            var result = Invoke(ref reference!, Intrinsics.NullAwareCast<TArgs>(args[1])!);
            args[0] = reference;
            return result;
        }

        /// <summary>
        /// Converts this pointer into <see cref="RefFunc{T, TArgs, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator RefFunc<T, TArgs, TResult>?(in ValueRefFunc<T, TArgs, TResult> pointer) => pointer.ToDelegate();

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
        public override bool Equals(object? other) => other is ValueRefFunc<T, TArgs, TResult> func && Equals(func);

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
