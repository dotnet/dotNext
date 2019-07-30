using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;
using M = InlineIL.MethodRef;
using TR = InlineIL.TypeRef;

namespace DotNext
{
    /// <summary>
    /// Represents a pointer to parameterless method with <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
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
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action action)
        {
            if(action is null)
                throw new ArgumentNullException(nameof(action));
            if(action.Method.IsAbstract || action.Target != null)
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
            Push(action);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Action), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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

            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void)));
            Ret();

            MarkLabel(callDelegate);
            Pop();
            Push(action);
            Tail();
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

        bool IEquatable<ValueAction>.Equals(ValueAction other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueAction other) => methodPtr == other.methodPtr && Equals(action, other.action);

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
        public static bool operator ==(in ValueAction first, in ValueAction second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction first, in ValueAction second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to parameterless method with return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
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
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<R> func)
        {
            if(func is null)
                throw new ArgumentNullException(nameof(func));
            if(func.Method.IsAbstract || func.Target != null)
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

        private ValueFunc(IntPtr methodPtr)
        {
            this.methodPtr = methodPtr;
            func = null;
        }

        private static R CreateDefault() => default;

        /// <summary>
        /// Returns activator for type <typeparamref name="R"/> in the form of typed method pointer.
        /// </summary>
        /// <remarks>
        /// Actual type <typeparamref name="R"/> should be a value type or have public parameterless constructor. 
        /// </remarks>
        public static ValueFunc<R> Activator
        {
            get
            {
                const string HandleRefType = "refType";
                Ldtoken(typeof(R));
                Call(new M(typeof(Type), nameof(Type.GetTypeFromHandle)));
                Call(M.PropertyGet(typeof(Type), nameof(Type.IsValueType)));
                Brfalse(HandleRefType);

                Call(M.PropertyGet(typeof(ValueFunc<R>), nameof(DefaultValueProvider)));
                Ret();

                MarkLabel(HandleRefType);
                Ldftn(new M(typeof(Activator), nameof(System.Activator.CreateInstance), Array.Empty<TR>()).MakeGenericMethod(typeof(R)));
                Newobj(M.Constructor(typeof(ValueFunc<R>), typeof(IntPtr)));
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
            get
            {
                Ldftn(new M(typeof(ValueFunc<R>), nameof(CreateDefault)));
                Newobj(M.Constructor(typeof(ValueFunc<R>), typeof(IntPtr)));
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
            Push(func);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Func<R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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

            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R)));
            Ret();

            MarkLabel(callDelegate);
            Pop();
            Push(func);
            Tail();
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

        bool IEquatable<ValueFunc<R>>.Equals(ValueFunc<R> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueFunc<R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

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
        public static bool operator ==(in ValueFunc<R> first, in ValueFunc<R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<R> first, in ValueFunc<R> second) => !first.Equals(second);
    }
    
    /// <summary>
    /// Represents a pointer to the predicate.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the predicate parameter.</typeparam>
    public readonly struct ValuePredicate<T> : ICallable<Predicate<T>>, IEquatable<ValuePredicate<T>>
    {
        private readonly ValueFunc<T, bool> func;

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
        public ValuePredicate(MethodInfo method) => func = new ValueFunc<T, bool>(method);

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the predicate.
        /// </summary>
        /// <remarks>
        /// You can use this constructor to create value delegate once and cache it using <c>static readonly</c> field
        /// for subsequent calls.
        /// </remarks>
        /// <param name="predicate">The predicate representing method.</param>
        public ValuePredicate(Predicate<T> predicate)
            => func = new ValueFunc<T, bool>(predicate.ChangeType<Func<T, bool>>());

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the predicate.
        /// </summary>
        /// <param name="func">The predicate representing method.</param>
        public ValuePredicate(Func<T, bool> func) => this.func = new ValueFunc<T, bool>(func);

        private ValuePredicate(IntPtr methodPtr) => func = new ValueFunc<T, bool>(methodPtr);

        /// <summary>
        /// Converts this typed pointer into <see cref="ValueFunc{T,R}"/>.
        /// </summary>
        /// <returns>The converted pointer.</returns>
        public ValueFunc<T, bool> Func => func;

        [SuppressMessage("Usage", "CA1801")]
        private static bool AlwaysTrue(T value) => true;

        [SuppressMessage("Usage", "CA1801")]
        private static bool AlwaysFalse(T value) => false;

        private static bool CheckNull(T value) => value == null;

        private static bool CheckNotNull(T value) => value != null;

        /// <summary>
        /// Gets pointer to the method determining whether the passed argument is <see langword="null"/>.
        /// </summary>
        public static ValuePredicate<T> IsNull
        {
            get
            {
                Ldftn(new M(typeof(ValuePredicate<T>), nameof(CheckNull)));
                Newobj(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr)));
                return Return<ValuePredicate<T>>();
            }
        }

        /// <summary>
        /// Gets pointer to the method determining whether the passed argument is not <see langword="null"/>.
        /// </summary>
        public static ValuePredicate<T> IsNotNull
        {
            get
            {
                Ldftn(new M(typeof(ValuePredicate<T>), nameof(CheckNotNull)));
                Newobj(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr)));
                return Return<ValuePredicate<T>>();
            }
        }

        /// <summary>
        /// Returns a predicate which always returns <see langword="true"/>.
        /// </summary>
        public static ValuePredicate<T> True
        {
            get
            {
                Ldftn(new M(typeof(ValuePredicate<T>), nameof(AlwaysTrue)));
                Newobj(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr)));
                return Return<ValuePredicate<T>>();
            }
        }

        /// <summary>
        /// Returns a predicate which always returns <see langword="false"/>.
        /// </summary>
        public static ValuePredicate<T> False
        {
            get
            {
                Ldftn(new M(typeof(ValuePredicate<T>), nameof(AlwaysFalse)));
                Newobj(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr)));
                return Return<ValuePredicate<T>>();
            }
        }

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func.Target;

        /// <summary>
        /// Converts this pointer into <see cref="Predicate{T}"/>.
        /// </summary>
        /// <returns>The predicate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Predicate<T> ToDelegate() => func.ToPredicateUnsafe();

        /// <summary>
        /// Spins until the condition represented by this predicate is satisfied.
        /// </summary>
        /// <remarks>
        /// The predicate has to be executed over and over until it returns true.
        /// </remarks>
        /// <param name="arg">The value to be passed into predicate.</param>
        public void SpinWait(T arg)
        {
            for (System.Threading.SpinWait spinner; !Invoke(arg); spinner.SpinOnce()) { }
        }

        /// <summary>
        /// Invokes predicate by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Invoke(T arg) => func.Invoke(arg);

        object ICallable.DynamicInvoke(params object[] args) => Invoke((T)args[0]);

        /// <summary>
        /// Converts this pointer into <see cref="Predicate{T}"/>.
        /// </summary>
        /// <param name="predicate">The pointer to convert.</param>
        /// <returns>The predicate created from this method pointer.</returns>
        public static explicit operator Predicate<T>(in ValuePredicate<T> predicate) => predicate.ToDelegate();

        /// <summary>
        /// Converts this typed pointer into <see cref="ValueFunc{T,R}"/>.
        /// </summary>
        /// <param name="predicate">The predicate to convert.</param>
        /// <returns>The converted pointer.</returns>
        public static implicit operator ValueFunc<T, bool>(ValuePredicate<T> predicate) => predicate.func;

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func.GetHashCode();

        bool IEquatable<ValuePredicate<T>>.Equals(ValuePredicate<T> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValuePredicate<T> other) => func == other.func;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => func.Equals(other);

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => func.ToString();

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValuePredicate<T> first, ValuePredicate<T> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValuePredicate<T> first, ValuePredicate<T> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct ValueFunc<T, R> : ICallable<Func<T, R>>, ICallable<Converter<T, R>>, IEquatable<ValueFunc<T, R>>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T, R> func)
        {
            if(func is null)
                throw new ArgumentNullException(nameof(func));
            if(func.Method.IsAbstract || func.Target != null)
            {
                this.func = func;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
                isStatic = func.Method.IsStatic;
            }
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="converter">The delegate representing method.</param>
        public ValueFunc(Converter<T, R> converter)
            : this(converter.ChangeType<Func<T, R>>())
        {
        }

        internal ValueFunc(IntPtr methodPtr)
        {
            this.methodPtr = methodPtr;
            func = null;
            isStatic = true;
        }

        Converter<T, R> ICallable<Converter<T, R>>.ToDelegate()
            => func is null ? ToConverter(methodPtr) : func.AsConverter();

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func?.Target;

        private static Converter<T, R> ToConverter(IntPtr methodPtr)
        {
            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Converter<T, R>), typeof(object), typeof(IntPtr)));
            return Return<Converter<T, R>>();
        }

        private static Predicate<T> ToPredicateUnsafe(IntPtr methodPtr)
        {
            Ldnull();
            Push(methodPtr);
            Newobj(M.Constructor(typeof(Predicate<T>), typeof(object), typeof(IntPtr)));
            return Return<Predicate<T>>();
        }

        internal Predicate<T> ToPredicateUnsafe() 
            => func is null ? ToPredicateUnsafe(methodPtr) : func.ChangeType<Predicate<T>>();

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T, R> ToDelegate()
        {
            const string returnDelegate = "delegate";
            Push(func);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Func<T, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg);
            Tail();
            Callvirt(new M(typeof(Func<T, R>), nameof(Invoke)));
            return Return<R>();
        }

        object ICallable.DynamicInvoke(params object[] args) => Invoke((T)args[0]);

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T, R>(in ValueFunc<T, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Converts this pointer into <see cref="Converter{T, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Converter<T, R>(in ValueFunc<T, R> pointer)
        {
            Push(nameof(pointer));
            Constrained(typeof(ValueFunc<T, R>));
            Callvirt(new M(typeof(ICallable<Converter<T, R>>), nameof(ICallable<Converter<T, R>>.ToDelegate)));
            return Return<Converter<T, R>>();
        }

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func?.GetHashCode() ?? methodPtr.GetHashCode();

        bool IEquatable<ValueFunc<T, R>>.Equals(ValueFunc<T, R> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueFunc<T, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

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
        public static bool operator ==(in ValueFunc<T, R> first, in ValueFunc<T, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T, R> first, in ValueFunc<T, R> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    public readonly struct ValueAction<T> : ICallable<Action<T>>, IEquatable<ValueAction<T>>, IConsumer<T>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T> action)
        {
            if(action is null)
                throw new ArgumentNullException(nameof(action));
            if(action.Method.IsAbstract || action.Target != null)
            {
                this.action = action;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
                isStatic = action.Method.IsStatic;
            }
        }

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
            Push(action);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Action<T>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
            return Return<Action<T>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        public void Invoke(T arg)
        {
            const string callDelegate = "delegate";
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg);
            Tail();
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

        bool IEquatable<ValueAction<T>>.Equals(ValueAction<T> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueAction<T> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueAction<T> action && Equals(in action);

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
        public static bool operator ==(in ValueAction<T> first, in ValueAction<T> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T> first, in ValueAction<T> second) => !first.Equals(second);
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
    public readonly struct ValueFunc<T1, T2, R> : ICallable<Func<T1, T2, R>>, IEquatable<ValueFunc<T1, T2, R>>, ISupplier<T1, T2, R>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, R> func)
        {
            if(func is null)
                throw new ArgumentNullException(nameof(func));
            if(func.Method.IsAbstract || func.Target != null)
            {
                this.func = func;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
                isStatic = func.Method.IsStatic;
            }
        }

        internal ValueFunc(IntPtr methodPtr)
        {
            this.methodPtr = methodPtr;
            func = null;
            isStatic = true;
        }

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
            Push(func);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Func<T1, T2, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T2)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Tail();
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

        bool IEquatable<ValueFunc<T1, T2, R>>.Equals(ValueFunc<T1, T2, R> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueFunc<T1, T2, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueFunc<T1, T2, R> func && Equals(in func);

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
        public static bool operator ==(in ValueFunc<T1, T2, R> first, in ValueFunc<T1, T2, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, R> first, in ValueFunc<T1, T2, R> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with two parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    public readonly struct ValueAction<T1, T2> : ICallable<Action<T1, T2>>, IEquatable<ValueAction<T1, T2>>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2> action)
        {
            if(action is null)
                throw new ArgumentNullException(nameof(action));
            if(action.Method.IsAbstract || action.Target != null)
            {
                this.action = action;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
                isStatic = action.Method.IsStatic;
            }
        }

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
            Push(action);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Action<T1, T2>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T2)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Tail();
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

        bool IEquatable<ValueAction<T1, T2>>.Equals(ValueAction<T1, T2> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueAction<T1, T2> other) => methodPtr == other.methodPtr && Equals(action, other.action);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="ICallable{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ValueAction<T1, T2> action && action.Equals(action);

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
        public static bool operator ==(in ValueAction<T1, T2> first, in ValueAction<T1, T2> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2> first, in ValueAction<T1, T2> second) => !first.Equals(second);
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
    public readonly struct ValueFunc<T1, T2, T3, R> : ICallable<Func<T1, T2, T3, R>>, IEquatable<ValueFunc<T1, T2, T3, R>>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, R> func)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (func.Method.IsAbstract || func.Target != null)
            {
                this.func = func;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
                isStatic = func.Method.IsStatic;
            }
        }

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
            Push(func);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T2), typeof(T3)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Tail();
            Callvirt(new M(typeof(Func<T1, T2, T3, R>), nameof(Invoke)));
            return Return<R>();
        }

        object ICallable.DynamicInvoke(params object[] args) => Invoke((T1)args[0], (T2)args[1], (T3)args[3]);

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

        bool IEquatable<ValueFunc<T1, T2, T3, R>>.Equals(ValueFunc<T1, T2, T3, R> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueFunc<T1, T2, T3, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

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
        public static bool operator ==(in ValueFunc<T1, T2, T3, R> first, in ValueFunc<T1, T2, T3, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, R> first, in ValueFunc<T1, T2, T3, R> second) => !first.Equals(second);
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
    public readonly struct ValueAction<T1, T2, T3> : ICallable<Action<T1, T2, T3>>, IEquatable<ValueAction<T1, T2, T3>>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3> action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (action.Method.IsAbstract || action.Target != null)
            {
                this.action = action;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
                isStatic = action.Method.IsStatic;
            }
        }

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
            Push(action);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T2), typeof(T3)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Tail();
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

        bool IEquatable<ValueAction<T1, T2, T3>>.Equals(ValueAction<T1, T2, T3> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueAction<T1, T2, T3> other) => methodPtr == other.methodPtr && Equals(action, other.action);

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
        public static bool operator ==(in ValueAction<T1, T2, T3> first, in ValueAction<T1, T2, T3> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3> first, in ValueAction<T1, T2, T3> second) => !first.Equals(second);
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
    public readonly struct ValueFunc<T1, T2, T3, T4, R> : ICallable<Func<T1, T2, T3, T4, R>>, IEquatable<ValueFunc<T1, T2, T3, T4, R>>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, T4, R> func)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (func.Method.IsAbstract || func.Target != null)
            {
                this.func = func;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
                isStatic = func.Method.IsStatic;
            }
        }

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
            Push(func);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, T4, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T2), typeof(T3), typeof(T4)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Tail();
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

        bool IEquatable<ValueFunc<T1, T2, T3, T4, R>>.Equals(ValueFunc<T1, T2, T3, T4, R> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueFunc<T1, T2, T3, T4, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

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
        public static bool operator ==(in ValueFunc<T1, T2, T3, T4, R> first, in ValueFunc<T1, T2, T3, T4, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, T4, R> first, in ValueFunc<T1, T2, T3, T4, R> second) => !first.Equals(second);
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
    public readonly struct ValueAction<T1, T2, T3, T4> : ICallable<Action<T1, T2, T3, T4>>, IEquatable<ValueAction<T1, T2, T3, T4>>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3, T4> action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (action.Method.IsAbstract || action.Target != null)
            {
                this.action = action;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
                isStatic = action.Method.IsStatic;
            }
        }

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
            Push(action);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3, T4>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T2), typeof(T3), typeof(T4)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Tail();
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

        bool IEquatable<ValueAction<T1, T2, T3, T4>>.Equals(ValueAction<T1, T2, T3, T4> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueAction<T1, T2, T3, T4> other) => methodPtr == other.methodPtr && Equals(action, other.action);

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
        public static bool operator ==(in ValueAction<T1, T2, T3, T4> first, in ValueAction<T1, T2, T3, T4> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3, T4> first, in ValueAction<T1, T2, T3, T4> second) => !first.Equals(second);
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
    public readonly struct ValueFunc<T1, T2, T3, T4, T5, R> : ICallable<Func<T1, T2, T3, T4, T5, R>>, IEquatable<ValueFunc<T1, T2, T3, T4, T5, R>>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public ValueFunc(Func<T1, T2, T3, T4, T5, R> func)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            if (func.Method.IsAbstract || func.Target != null)
            {
                this.func = func;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.func = null;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
                isStatic = func.Method.IsStatic;
            }
        }

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
            Push(func);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, T4, T5, R>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();

            MarkLabel(callDelegate);
            Push(func);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Tail();
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

        bool IEquatable<ValueFunc<T1, T2, T3, T4, T5, R>>.Equals(ValueFunc<T1, T2, T3, T4, T5, R> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueFunc<T1, T2, T3, T4, T5, R> other) => methodPtr == other.methodPtr && Equals(func, other.func);

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
        public static bool operator ==(in ValueFunc<T1, T2, T3, T4, T5, R> first, in ValueFunc<T1, T2, T3, T4, T5, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueFunc<T1, T2, T3, T4, T5, R> first, in ValueFunc<T1, T2, T3, T4, T5, R> second) => !first.Equals(second);
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
    public readonly struct ValueAction<T1, T2, T3, T4, T5> : ICallable<Action<T1, T2, T3, T4, T5>>, IEquatable<ValueAction<T1, T2, T3, T4, T5>>
    {
        private readonly IntPtr methodPtr;
        private readonly bool isStatic;
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
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public ValueAction(Action<T1, T2, T3, T4, T5> action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            if (action.Method.IsAbstract || action.Target != null)
            {
                this.action = action;
                methodPtr = default;
                isStatic = default;
            }
            else
            {
                this.action = null;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
                isStatic = action.Method.IsStatic;
            }
        }

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
            Push(action);
            Push(methodPtr);
            Dup();
            Brfalse(returnDelegate);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3, T4, T5>), typeof(object), typeof(IntPtr)));
            Ret();

            MarkLabel(returnDelegate);
            Pop();
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
            const string callInstance = "instance";
            Push(methodPtr);
            Brfalse(callDelegate);

            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Push(methodPtr);
            Push(isStatic);

            Brfalse(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();

            MarkLabel(callInstance);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();

            MarkLabel(callDelegate);
            Push(action);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Tail();
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

        bool IEquatable<ValueAction<T1, T2, T3, T4, T5>>.Equals(ValueAction<T1, T2, T3, T4, T5> other) => Equals(other);

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public bool Equals(in ValueAction<T1, T2, T3, T4, T5> other) => methodPtr == other.methodPtr && Equals(action, other.action);

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
        public static bool operator ==(in ValueAction<T1, T2, T3, T4, T5> first, in ValueAction<T1, T2, T3, T4, T5> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ValueAction<T1, T2, T3, T4, T5> first, in ValueAction<T1, T2, T3, T4, T5> second) => !first.Equals(second);
    }
}
