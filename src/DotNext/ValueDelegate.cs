using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext
{
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
    public readonly unsafe struct ValueAction : IValueDelegate<Action>, IEquatable<ValueAction>
    {
        private readonly delegate*<void> methodPtr;
        private readonly Action? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueAction(delegate*<void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        public void Invoke()
        {
            if (methodPtr == null)
                action!();
            else
                methodPtr();
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueAction other)
            => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueAction action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction first, in ValueAction second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction first, in ValueAction second)
            => !first.Equals(in second);
    }

    /// <summary>
    /// Represents a pointer to parameterless method with return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="TResult">The type of the return value of the method that this pointer encapsulates.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ValueFunc<TResult> : IValueDelegate<Func<TResult>>, IEquatable<ValueFunc<TResult>>, ISupplier<TResult>
    {
        private readonly delegate*<TResult> methodPtr;
        private readonly Func<TResult>? func;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="func">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<TResult> func)
        {
            this.func = func ?? throw new ArgumentNullException(nameof(func));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="func">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueFunc(delegate*<TResult> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            methodPtr = func;
            this.func = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == null;

        /// <summary>
        /// Returns activator for type <typeparamref name="TResult"/> in the form of typed method pointer.
        /// </summary>
        /// <remarks>
        /// Actual type <typeparamref name="TResult"/> should be a value type or have public parameterless constructor.
        /// </remarks>
        public static ValueFunc<TResult> Activator => new ValueFunc<TResult>(&System.Activator.CreateInstance<TResult>);

        /// <summary>
        /// Obtains pointer to the method that returns <see langword="null"/> if <typeparamref name="TResult"/>
        /// is reference type or initialized value type if <typeparamref name="TResult"/> is value type.
        /// </summary>
        public static ValueFunc<TResult?> DefaultValueProvider => new ValueFunc<TResult?>(&Intrinsics.DefaultOf<TResult>);

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<TResult>? ToDelegate() => func ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke()
            => methodPtr == null ? func!() : methodPtr();

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args) => Invoke();

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
        public override int GetHashCode() => func?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueFunc<TResult> other)
            => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<TResult> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<TResult> func && Equals(in func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<TResult> first, in ValueFunc<TResult> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<TResult> first, in ValueFunc<TResult> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueFunc<T, TResult> : IValueDelegate<Func<T, TResult>>, IValueDelegate<Converter<T, TResult>>, IEquatable<ValueFunc<T, TResult>>
    {
        private readonly delegate*<T, TResult> methodPtr;
        private readonly Func<T, TResult>? func;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="func">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T, TResult> func)
        {
            this.func = func ?? throw new ArgumentNullException(nameof(func));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="func">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueFunc(delegate*<T, TResult> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            methodPtr = func;
            this.func = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == null;

        private Converter<T, TResult>? ToConverter()
            => Unsafe.As<Converter<T, TResult>>(func) ?? DelegateHelpers.CreateConverter(methodPtr);

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
        public Func<T, TResult>? ToDelegate() => func ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke(T arg)
            => methodPtr == null ? func!(arg) : methodPtr(arg);

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args) => Invoke(Intrinsics.NullAwareCast<T>(args[0])!);

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
        public override int GetHashCode() => func?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueFunc<T, TResult> other)
            => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T, TResult> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T, TResult> func && Equals(in func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T, TResult> first, in ValueFunc<T, TResult> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T, TResult> first, in ValueFunc<T, TResult> second)
            => !first.Equals(in second);
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ValueAction<T> : IValueDelegate<Action<T>>, IEquatable<ValueAction<T>>, IConsumer<T>
    {
        private readonly delegate*<T, void> methodPtr;
        private readonly Action<T>? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueAction(delegate*<T, void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T>? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        public void Invoke(T arg)
        {
            if (methodPtr == null)
                action!(arg);
            else
                methodPtr(arg);
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueAction<T> other)
            => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueAction<T> action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T> first, in ValueAction<T> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T> first, in ValueAction<T> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueFunc<T1, T2, TResult> : IValueDelegate<Func<T1, T2, TResult>>, IEquatable<ValueFunc<T1, T2, TResult>>, ISupplier<T1, T2, TResult>
    {
        private readonly delegate*<T1, T2, TResult> methodPtr;
        private readonly Func<T1, T2, TResult>? func;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="func">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, TResult> func)
        {
            this.func = func ?? throw new ArgumentNullException(nameof(func));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="func">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueFunc(delegate*<T1, T2, TResult> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            methodPtr = func;
            this.func = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, TResult>? ToDelegate() => func ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke(T1 arg1, T2 arg2)
            => methodPtr == null ? func!(arg1, arg2) : methodPtr(arg1, arg2);

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => func?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueFunc<T1, T2, TResult> other)
            => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T1, T2, TResult> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T1, T2, TResult> func && Equals(in func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T1, T2, TResult> first, in ValueFunc<T1, T2, TResult> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, TResult> first, in ValueFunc<T1, T2, TResult> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueAction<T1, T2> : IValueDelegate<Action<T1, T2>>, IEquatable<ValueAction<T1, T2>>
    {
        private readonly delegate*<T1, T2, void> methodPtr;
        private readonly Action<T1, T2>? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueAction(delegate*<T1, T2, void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2>? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        public void Invoke(T1 arg1, T2 arg2)
        {
            if (methodPtr == null)
                action!(arg1, arg2);
            else
                methodPtr(arg1, arg2);
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueAction<T1, T2> other)
            => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T1, T2> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueAction<T1, T2> action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T1, T2> first, in ValueAction<T1, T2> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2> first, in ValueAction<T1, T2> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueFunc<T1, T2, T3, TResult> : IValueDelegate<Func<T1, T2, T3, TResult>>, IEquatable<ValueFunc<T1, T2, T3, TResult>>
    {
        private readonly delegate*<T1, T2, T3, TResult> methodPtr;
        private readonly Func<T1, T2, T3, TResult>? func;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="func">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, TResult> func)
        {
            this.func = func ?? throw new ArgumentNullException(nameof(func));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="func">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueFunc(delegate*<T1, T2, T3, TResult> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            methodPtr = func;
            this.func = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, TResult>? ToDelegate() => func ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke(T1 arg1, T2 arg2, T3 arg3)
            => methodPtr == null ? func!(arg1, arg2, arg3) : methodPtr(arg1, arg2, arg3);

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => func?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueFunc<T1, T2, T3, TResult> other)
            => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T1, T2, T3, TResult> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T1, T2, T3, TResult> func && Equals(in func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T1, T2, T3, TResult> first, in ValueFunc<T1, T2, T3, TResult> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, TResult> first, in ValueFunc<T1, T2, T3, TResult> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueAction<T1, T2, T3> : IValueDelegate<Action<T1, T2, T3>>, IEquatable<ValueAction<T1, T2, T3>>
    {
        private readonly delegate*<T1, T2, T3, void> methodPtr;
        private readonly Action<T1, T2, T3>? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueAction(delegate*<T1, T2, T3, void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2,T3}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3>? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        public void Invoke(T1 arg1, T2 arg2, T3 arg3)
        {
            if (methodPtr == null)
                action!(arg1, arg2, arg3);
            else
                methodPtr(arg1, arg2, arg3);
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueAction<T1, T2, T3> other)
            => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T1, T2, T3> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueAction<T1, T2, T3> action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T1, T2, T3> first, in ValueAction<T1, T2, T3> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3> first, in ValueAction<T1, T2, T3> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueFunc<T1, T2, T3, T4, TResult> : IValueDelegate<Func<T1, T2, T3, T4, TResult>>, IEquatable<ValueFunc<T1, T2, T3, T4, TResult>>
    {
        private readonly delegate*<T1, T2, T3, T4, TResult> methodPtr;
        private readonly Func<T1, T2, T3, T4, TResult>? func;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="func">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, T4, TResult> func)
        {
            this.func = func ?? throw new ArgumentNullException(nameof(func));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="func">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueFunc(delegate*<T1, T2, T3, T4, TResult> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            methodPtr = func;
            this.func = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, TResult>? ToDelegate() => func ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <param name="arg4">The fourth argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public TResult Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => methodPtr == null ? func!(arg1, arg2, arg3, arg4) : methodPtr(arg1, arg2, arg3, arg4);

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => func?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueFunc<T1, T2, T3, T4, TResult> other)
            => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T1, T2, T3, T4, TResult> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T1, T2, T3, T4, TResult> func && Equals(in func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T1, T2, T3, T4, TResult> first, in ValueFunc<T1, T2, T3, T4, TResult> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, T4, TResult> first, in ValueFunc<T1, T2, T3, T4, TResult> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueAction<T1, T2, T3, T4> : IValueDelegate<Action<T1, T2, T3, T4>>, IEquatable<ValueAction<T1, T2, T3, T4>>
    {
        private readonly delegate*<T1, T2, T3, T4, void> methodPtr;
        private readonly Action<T1, T2, T3, T4>? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3, T4> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueAction(delegate*<T1, T2, T3, T4, void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2,T3,T4}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4>? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg1">The first argument to be passed into the target method.</param>
        /// <param name="arg2">The second argument to be passed into the target method.</param>
        /// <param name="arg3">The third argument to be passed into the target method.</param>
        /// <param name="arg4">The fourth argument to be passed into the target method.</param>
        public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (methodPtr == null)
                action!(arg1, arg2, arg3, arg4);
            else
                methodPtr(arg1, arg2, arg3, arg4);
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueAction<T1, T2, T3, T4> other)
            => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T1, T2, T3, T4> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueAction<T1, T2, T3, T4> action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T1, T2, T3, T4> first, in ValueAction<T1, T2, T3, T4> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3, T4> first, in ValueAction<T1, T2, T3, T4> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueFunc<T1, T2, T3, T4, T5, TResult> : IValueDelegate<Func<T1, T2, T3, T4, T5, TResult>>, IEquatable<ValueFunc<T1, T2, T3, T4, T5, TResult>>
    {
        private readonly delegate*<T1, T2, T3, T4, T5, TResult> methodPtr;
        private readonly Func<T1, T2, T3, T4, T5, TResult>? func;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="func">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, T4, T5, TResult> func)
        {
            this.func = func ?? throw new ArgumentNullException(nameof(func));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="func">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueFunc(delegate*<T1, T2, T3, T4, T5, TResult> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            methodPtr = func;
            this.func = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, T5, TResult>? ToDelegate() => func ?? DelegateHelpers.CreateDelegate(methodPtr);

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
            => methodPtr == null ? func!(arg1, arg2, arg3, arg4, arg5) : methodPtr(arg1, arg2, arg3, arg4, arg5);

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => func?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueFunc<T1, T2, T3, T4, T5, TResult> other)
            => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueFunc<T1, T2, T3, T4, T5, TResult> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueFunc<T1, T2, T3, T4, T5, TResult> func && Equals(in func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueFunc<T1, T2, T3, T4, T5, TResult> first, in ValueFunc<T1, T2, T3, T4, T5, TResult> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, T4, T5, TResult> first, in ValueFunc<T1, T2, T3, T4, T5, TResult> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueAction<T1, T2, T3, T4, T5> : IValueDelegate<Action<T1, T2, T3, T4, T5>>, IEquatable<ValueAction<T1, T2, T3, T4, T5>>
    {
        private readonly delegate*<T1, T2, T3, T4, T5, void> methodPtr;
        private readonly Action<T1, T2, T3, T4, T5>? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3, T4, T5> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueAction(delegate*<T1, T2, T3, T4, T5, void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4, T5>? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

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
            if (methodPtr == null)
                action!(arg1, arg2, arg3, arg4, arg5);
            else
                methodPtr(arg1, arg2, arg3, arg4, arg5);
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueAction<T1, T2, T3, T4, T5> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueAction<T1, T2, T3, T4, T5> other)
            => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueAction<T1, T2, T3, T4, T5> action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueAction<T1, T2, T3, T4, T5> first, in ValueAction<T1, T2, T3, T4, T5> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3, T4, T5> first, in ValueAction<T1, T2, T3, T4, T5> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueRefAction<T, TArgs> : IValueDelegate<RefAction<T, TArgs>>, IEquatable<ValueRefAction<T, TArgs>>
    {
        private readonly delegate*<ref T, TArgs, void> methodPtr;
        private readonly RefAction<T, TArgs>? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueRefAction(RefAction<T, TArgs> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueRefAction(delegate*<ref T, TArgs, void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="RefAction{T, TArgs}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public RefAction<T, TArgs>? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="reference">The object passed by reference.</param>
        /// <param name="args">The action arguments.</param>
        public void Invoke(ref T reference, TArgs args)
        {
            if (methodPtr == null)
                action!(ref reference, args);
            else
                methodPtr(ref reference, args);
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueRefAction<T, TArgs> other)
            => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueRefAction<T, TArgs> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueRefAction<T, TArgs> action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueRefAction<T, TArgs> first, in ValueRefAction<T, TArgs> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueRefAction<T, TArgs> first, in ValueRefAction<T, TArgs> second)
            => !first.Equals(in second);
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
    public readonly unsafe struct ValueRefFunc<T, TArgs, TResult> : IValueDelegate<RefFunc<T, TArgs, TResult>>, IEquatable<ValueRefFunc<T, TArgs, TResult>>
    {
        private readonly delegate*<ref T, TArgs, TResult> methodPtr;
        private readonly RefFunc<T, TArgs, TResult>? func;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="func">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueRefFunc(RefFunc<T, TArgs, TResult> func)
        {
            this.func = func ?? throw new ArgumentNullException(nameof(func));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="func">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueRefFunc(delegate*<ref T, TArgs, TResult> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            methodPtr = func;
            this.func = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => func is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => func?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="RefFunc{T, TArgs, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public RefFunc<T, TArgs, TResult>? ToDelegate() => func ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="reference">The object passed by reference.</param>
        /// <param name="args">The action arguments.</param>
        /// <returns>The value returned by underlying method.</returns>
        public TResult Invoke(ref T reference, TArgs args)
            => methodPtr == null ? func!(ref reference, args) : methodPtr(ref reference, args);

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
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
        public override int GetHashCode() => func?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueRefFunc<T, TArgs, TResult> other)
            => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueRefFunc<T, TArgs, TResult> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueRefFunc<T, TArgs, TResult> func && Equals(in func);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueRefFunc<T, TArgs, TResult> first, in ValueRefFunc<T, TArgs, TResult> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueRefFunc<T, TArgs, TResult> first, in ValueRefFunc<T, TArgs, TResult> second)
            => !first.Equals(in second);
    }

    /// <summary>
    /// Represents a value delegate compatible with <see cref="ReadOnlySpanAction{T, TArg}"/> delegate type.
    /// </summary>
    /// <typeparam name="T">The type of the objects in the span.</typeparam>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ValueReadOnlySpanAction<T, TArg> : IValueDelegate<ReadOnlySpanAction<T, TArg>>, IEquatable<ValueReadOnlySpanAction<T, TArg>>
    {
        private readonly delegate*<ReadOnlySpan<T>, TArg, void> methodPtr;
        private readonly ReadOnlySpanAction<T, TArg>? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueReadOnlySpanAction(ReadOnlySpanAction<T, TArg> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueReadOnlySpanAction(delegate*<ReadOnlySpan<T>, TArg, void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public ReadOnlySpanAction<T, TArg>? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="span">A read-only span of objects.</param>
        /// <param name="arg">A state object.</param>
        public void Invoke(ReadOnlySpan<T> span, TArg arg)
        {
            if (methodPtr == null)
                action!(span, arg);
            else
                methodPtr(span, arg);
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
        {
            Invoke(new ReadOnlySpan<T>((T[]?)args[0]), Intrinsics.NullAwareCast<TArg>(args[1])!);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator ReadOnlySpanAction<T, TArg>?(in ValueReadOnlySpanAction<T, TArg> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueReadOnlySpanAction<T, TArg> other)
            => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueReadOnlySpanAction<T, TArg> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueReadOnlySpanAction<T, TArg> action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueReadOnlySpanAction<T, TArg> first, in ValueReadOnlySpanAction<T, TArg> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueReadOnlySpanAction<T, TArg> first, in ValueReadOnlySpanAction<T, TArg> second)
            => !first.Equals(in second);
    }

    /// <summary>
    /// Represents a value delegate compatible with <see cref="SpanAction{T, TArg}"/> delegate type.
    /// </summary>
    /// <typeparam name="T">The type of the objects in the span.</typeparam>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ValueSpanAction<T, TArg> : IValueDelegate<SpanAction<T, TArg>>, IEquatable<ValueSpanAction<T, TArg>>
    {
        private readonly delegate*<Span<T>, TArg, void> methodPtr;
        private readonly SpanAction<T, TArg>? action;

        /// <summary>
        /// Wraps delegate instance.
        /// </summary>
        /// <param name="action">The delegate representing the method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueSpanAction(SpanAction<T, TArg> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            methodPtr = null;
        }

        /// <summary>
        /// Wraps function pointer.
        /// </summary>
        /// <param name="action">The pointer to the static method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public ValueSpanAction(delegate*<Span<T>, TArg, void> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            methodPtr = action;
            this.action = null;
        }

        /// <summary>
        /// Indicates that this delegate doesn't refer to any method.
        /// </summary>
        public bool IsEmpty => action is null && methodPtr == null;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object? Target => action?.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public SpanAction<T, TArg>? ToDelegate() => action ?? DelegateHelpers.CreateDelegate(methodPtr);

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="span">A read-only span of objects.</param>
        /// <param name="arg">A state object.</param>
        public void Invoke(Span<T> span, TArg arg)
        {
            if (methodPtr == null)
                action!(span, arg);
            else
                methodPtr(span, arg);
        }

        /// <inheritdoc/>
        object? ICallable.DynamicInvoke(Span<object?> args)
        {
            Invoke(new Span<T>((T[]?)args[0]), Intrinsics.NullAwareCast<TArg>(args[1])!);
            return null;
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator SpanAction<T, TArg>?(in ValueSpanAction<T, TArg> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => action?.GetHashCode() ?? Intrinsics.PointerHashCode(methodPtr);

        private bool Equals(in ValueSpanAction<T, TArg> other)
            => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValueSpanAction<T, TArg> other) => Equals(in other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ValueSpanAction<T, TArg> action && Equals(in action);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => action?.ToString() ?? new IntPtr(methodPtr).ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ValueSpanAction<T, TArg> first, in ValueSpanAction<T, TArg> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueSpanAction<T, TArg> first, in ValueSpanAction<T, TArg> second)
            => !first.Equals(in second);
    }
}
