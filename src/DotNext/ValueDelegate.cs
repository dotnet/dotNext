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
    using IMethodCookieSupport = Reflection.IMethodCookieSupport;

    internal enum DelegationType : uint
    {
        None = 0,

        OpenStaticMethod = 1,

        ClosedStaticMethod = 2,

        OpenInstanceMethod = 3,

        ClosedInstanceMethod = 4
    }

    /// <summary>
    /// Represents a pointer to parameterless method with <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueAction : IMethodPointer<Action>, IEquatable<ValueAction>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="action">The delegate representing method.</param>
        public ValueAction(Action action)
        {
            if(action.Method.IsAbstract)
            {
                target = action;
                methodPtr = default;
            }
            else
            {
                target = action.Target;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueAction(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueAction), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Action>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Action));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Action), typeof(object), typeof(IntPtr)));
            return Return<Action>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueAction<G> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueAction<G>(methodPtr, null);
            if(target is Action action && methodPtr == IntPtr.Zero)
                return new ValueAction<G>(action.Unbind<G>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        public void Invoke()
        {
            const string callIndirect = "indirect";
            const string callImplicitThis = "implicitThis";
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Tail();
            Callvirt(new M(typeof(Action), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
            Dup();
            Brtrue(callImplicitThis);
            Pop();
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void)));
            Ret();
            MarkLabel(callImplicitThis);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action(ValueAction pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueAction other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueAction first, ValueAction second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueAction first, ValueAction second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to parameterless method with return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueFunc<R> : IMethodPointer<Func<R>>, IEquatable<ValueFunc<R>>, ISupplier<R>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D, P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="func">The delegate representing method.</param>
        public ValueFunc(Func<R> func)
        {
            if(func.Method.IsAbstract)
            {
                target = func;
                methodPtr = default;
            }
            else
            {
                target = func.Target;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueFunc(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueFunc<R>), typeof(IntPtr), typeof(object)));

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
                Ldnull();
                Newobj(M.Constructor(typeof(ValueFunc<R>), typeof(IntPtr), typeof(object)));
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
                Ldnull();
                Newobj(M.Constructor(typeof(ValueFunc<R>), typeof(IntPtr), typeof(object)));
                return Return<ValueFunc<R>>();
            }
        }

        IntPtr IMethodPointer<Func<R>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Func<R>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Func<R>), typeof(object), typeof(IntPtr)));
            return Return<Func<R>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueFunc<G, R> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueFunc<G, R>(methodPtr, null);
            if(target is Func<R> func && methodPtr == IntPtr.Zero)
                return new ValueFunc<G, R>(func.Unbind<G, R>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <returns>The result of method invocation.</returns>
        public R Invoke()
        {
            const string callIndirect = "indirect";
            const string callImplicitThis = "implicitThis";
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Tail();
            Callvirt(new M(typeof(Func<R>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
            Dup();
            Brtrue(callImplicitThis);
            Pop();
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R)));
            Ret();
            MarkLabel(callImplicitThis);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<R>(ValueFunc<R> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueFunc<R> first, ValueFunc<R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueFunc<R> first, ValueFunc<R> second) => !first.Equals(second);
    }
    
    /// <summary>
    /// Represents a pointer to the predicate.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument)
    /// </remarks>
    /// <typeparam name="T">The type of the predicate parameter.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValuePredicate<T> : IMethodPointer<Predicate<T>>, IEquatable<ValuePredicate<T>>, IMethodCookieSupport
    {
        private readonly ValueFunc<T, bool> func;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValuePredicate(MethodInfo method, object target = null) => func = new ValueFunc<T, bool>(method, target);

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the predicate.
        /// </summary>
        /// <param name="predicate">The predicate representing method.</param>
        public ValuePredicate(Predicate<T> predicate)
            => func = predicate.Method.IsAbstract ? new ValueFunc<T, bool>(default(IntPtr), predicate.ChangeType<Func<T, bool>>()) : new ValueFunc<T, bool>(predicate.Method.MethodHandle.GetFunctionPointer(), predicate.Target);

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the predicate.
        /// </summary>
        /// <param name="func">The predicate representing method.</param>
        public ValuePredicate(Func<T, bool> func) => this.func = new ValueFunc<T, bool>(func);

        private ValuePredicate(IntPtr methodPtr, object target) => func = new ValueFunc<T, bool>(methodPtr, target);

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr), typeof(object)));

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
                Ldnull();
                Newobj(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr), typeof(object)));
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
                Ldnull();
                Newobj(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr), typeof(object)));
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
                Ldnull();
                Newobj(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr), typeof(object)));
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
                Ldnull();
                Newobj(M.Constructor(typeof(ValuePredicate<T>), typeof(IntPtr), typeof(object)));
                return Return<ValuePredicate<T>>();
            }
        }

        IntPtr IMethodPointer<Predicate<T>>.Address => func.Address;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => func.Target;

        private static Predicate<T> ToDelegate(IntPtr methodPtr, object target)
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Predicate<T>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Predicate<T>), typeof(object), typeof(IntPtr)));
            return Return<Predicate<T>>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Predicate{T}"/>.
        /// </summary>
        /// <returns>The predicate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Predicate<T> ToDelegate() => ToDelegate(func.Address, Target);

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueFunc<G, T, bool> Unbind<G>() where G : class => func.Unbind<G>();

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueFunc<bool> Bind<G>(G obj) where G : class, T => func.Bind(obj);

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

        /// <summary>
        /// Converts this pointer into <see cref="Predicate{T}"/>.
        /// </summary>
        /// <param name="predicate">The pointer to convert.</param>
        /// <returns>The predicate created from this method pointer.</returns>
        public static explicit operator Predicate<T>(ValuePredicate<T> predicate) => predicate.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => func.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ValuePredicate<T> other) => func == other.func;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
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
        public static bool operator !=(ValuePredicate<T> first, ValuePredicate<T> second) => first.Equals(second);

        /// <summary>
        /// Converts this typed pointer into <see cref="ValueFunc{T,R}"/>.
        /// </summary>
        /// <param name="predicate">The predicate to convert.</param>
        /// <returns>The converted pointer.</returns>
        public static implicit operator ValueFunc<T, bool>(ValuePredicate<T> predicate) => predicate.func;
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueFunc<T, R> : IMethodPointer<Func<T, R>>, IMethodPointer<Converter<T, R>>, IEquatable<ValueFunc<T, R>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;
        private readonly DelegationType type;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="func">The delegate representing method.</param>
        public ValueFunc(Func<T, R> func)
        {
            if(func.Method.IsAbstract)
            {
                target = func;
                methodPtr = default;
                type = DelegationType.None;
            }
            else
            {
                target = func.Target;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
                type = func.Method.IsStatic ?
                    (target is null ? DelegationType.OpenStaticMethod : DelegationType.ClosedStaticMethod) :
                    (target is null ? DelegationType.OpenInstanceMethod : DelegationType.ClosedInstanceMethod);
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

        internal ValueFunc(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
            type = DelegationType.None;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueFunc<T, R>), typeof(IntPtr), typeof(object)));

        internal IntPtr Address => methodPtr;

        IntPtr IMethodPointer<Func<T, R>>.Address => methodPtr;
        IntPtr IMethodPointer<Converter<T, R>>.Address => methodPtr;
        Converter<T, R> IMethodPointer<Converter<T, R>>.ToDelegate() => ToConverter();

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Func<T, R>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Func<T, R>), typeof(object), typeof(IntPtr)));
            return Return<Func<T, R>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueFunc<G, T, R> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueFunc<G, T, R>(methodPtr, null);
            if(target is Func<T, R> func && methodPtr == IntPtr.Zero)
                return new ValueFunc<G, T, R>(func.Unbind<G, T, R>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueFunc<R> Bind<G>(G obj)
            where G : class, T
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueFunc<R>(methodPtr, obj);
                case Func<T, R> func when methodPtr == IntPtr.Zero:
                    return new ValueFunc<R>(func.Bind<G, R>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Converts this pointer into <see cref="Converter{T, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Converter<T, R> ToConverter()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Converter<T, R>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Converter<T, R>), typeof(object), typeof(IntPtr)));
            return Return<Converter<T, R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        /// <returns>The result of method invocation.</returns>
        public R Invoke(T arg)
        {
            const string callDelegate = "delegate";
            const string openStatic = "openStatic";
            const string closedStatic = "closedStatic";
            const string openInstance = "openInstance";
            const string closedInstance = "closedInstance";
            Push(type);
            Switch(callDelegate, openStatic, closedStatic, openInstance, closedInstance);
            
            //redirect call to delegate
            MarkLabel(callDelegate);
            Push(target);
            Push(arg);
            Tail();
            Callvirt(new M(typeof(Func<T, R>), nameof(Invoke)));
            Ret();

            //open static method
            MarkLabel(openStatic);
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T)));
            Ret();

            //closed static method
            MarkLabel(closedStatic);
            Push(target);
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(object), typeof(T)));
            Ret();

            //open instance method
            MarkLabel(openInstance);
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R)));
            Ret();

            MarkLabel(closedInstance);
            Push(target);
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T, R>(ValueFunc<T, R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Converts this pointer into <see cref="Converter{T, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Converter<T, R>(ValueFunc<T, R> pointer) => pointer.ToConverter();

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
        public bool Equals(ValueFunc<T, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueFunc<T, R> first, ValueFunc<T, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueFunc<T, R> first, ValueFunc<T, R> second) => first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueAction<T> : IMethodPointer<Action<T>>, IEquatable<ValueAction<T>>, IMethodCookieSupport, IConsumer<T>
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="action">The delegate representing method.</param>
        public ValueAction(Action<T> action)
        {
            if(action.Method.IsAbstract)
            {
                methodPtr = default;
                target = action;
            }
            else
            {
                target = action.Target;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueAction(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void Reflection.IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueAction<T>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Action<T>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Action<T>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Action<T>), typeof(object), typeof(IntPtr)));
            return Return<Action<T>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueAction<G, T> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueAction<G, T>(methodPtr, null);
            if(target is Action<T> action && methodPtr == IntPtr.Zero)
                return new ValueAction<G, T>(action.Unbind<G, T>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueAction Bind<G>(G obj)
            where G : class, T
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueAction(methodPtr, obj);
                case Action<T> action when methodPtr == IntPtr.Zero:
                    return new ValueAction(action.Bind<G>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <param name="arg">The first argument to be passed into the target method.</param>
        public void Invoke(T arg)
        {
            const string callIndirect = "indirect";
            const string callImplicitThis = "implicitThis";
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg);
            Tail();
            Callvirt(new M(typeof(Action<T>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T>(ValueAction<T> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueAction<T> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueAction<T> first, ValueAction<T> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueAction<T> first, ValueAction<T> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with two parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueFunc<T1, T2, R> : IMethodPointer<Func<T1, T2, R>>, IEquatable<ValueFunc<T1, T2, R>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T1, T2, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="func">The delegate representing method.</param>
        public ValueFunc(Func<T1, T2, R> func)
        {
            if(func.Method.IsAbstract)
            {
                target = func;
                methodPtr = default;
            }
            else
            {
                target = func.Target;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueFunc(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void Reflection.IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueFunc<T1, T2, R>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Func<T1, T2, R>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Func<T1, T2, R>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Func<T1, T2, R>), typeof(object), typeof(IntPtr)));
            return Return<Func<T1, T2, R>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueFunc<G, T1, T2, R> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueFunc<G, T1, T2, R>(methodPtr, null);
            if(target is Func<T1, T2, R> func && methodPtr == IntPtr.Zero)
                return new ValueFunc<G, T1, T2, R>(func.Unbind<G, T1, T2, R>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueFunc<T2, R> Bind<G>(G obj)
            where G : class, T1
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueFunc<T2, R>(methodPtr, obj);
                case Func<T1, T2, R> func when methodPtr == IntPtr.Zero:
                    return new ValueFunc<T2, R>(func.Bind<G, T2, R>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
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
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg1);
            Push(arg2);
            Tail();
            Callvirt(new M(typeof(Func<T1, T2, R>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T1), typeof(T2)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, R>(ValueFunc<T1, T2, R> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<T1, T2, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueFunc<T1, T2, R> first, ValueFunc<T1, T2, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueFunc<T1, T2, R> first, ValueFunc<T1, T2, R> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with two parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueAction<T1, T2> : IMethodPointer<Action<T1, T2>>, IEquatable<ValueAction<T1, T2>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T1, T2>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="action">The delegate representing method.</param>
        public ValueAction(Action<T1, T2> action)
        {
            if(action.Method.IsAbstract)
            {
                target = action;
                methodPtr = default;
            }
            else
            {
                target = action.Target;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueAction(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void Reflection.IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueAction<T1, T2>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Action<T1, T2>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Action<T1, T2>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Action<T1, T2>), typeof(object), typeof(IntPtr)));
            return Return<Action<T1, T2>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueAction<G, T1, T2> Unbind<G>()
            where G : class
         {
             if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueAction<G, T1, T2>(methodPtr, null);
            if(target is Action<T1, T2> action && methodPtr == IntPtr.Zero)
                return new ValueAction<G, T1, T2>(action.Unbind<G, T1, T2>());
            throw new InvalidOperationException();
         }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueAction<T2> Bind<G>(G obj)
            where G : class, T1
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueAction<T2>(methodPtr, obj);
                case Action<T1, T2> action when methodPtr == IntPtr.Zero:
                    return new ValueAction<T2>(action.Bind<G, T2>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
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
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg1);
            Push(arg2);
            Tail();
            Callvirt(new M(typeof(Action<T1, T2>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T1), typeof(T2)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2>(ValueAction<T1, T2> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueAction<T1, T2> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueAction<T1, T2> first, ValueAction<T1, T2> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueAction<T1, T2> first, ValueAction<T1, T2> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with three parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueFunc<T1, T2, T3, R> : IMethodPointer<Func<T1, T2, T3, R>>, IEquatable<ValueFunc<T1, T2, T3, R>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T1, T2, T3, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="func">The delegate representing method.</param>
        public ValueFunc(Func<T1, T2, T3, R> func)
        {
            if(func.Method.IsAbstract)
            {
                target = func;
                methodPtr = default;
            }
            else
            {
                target = func.Target;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueFunc(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueFunc<T1, T1, T3, R>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Func<T1, T2, T3, R>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Func<T1, T2, T3, R>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, R>), typeof(object), typeof(IntPtr)));
            return Return<Func<T1, T2, T3, R>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueFunc<G, T1, T2, T3, R> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueFunc<G, T1, T2, T3, R>(methodPtr, null);
            if(target is Func<T1, T2, T3, R> func && methodPtr == IntPtr.Zero)
                return new ValueFunc<G, T1, T2, T3, R>(func.Unbind<G, T1, T2, T3, R>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueFunc<T2, T3, R> Bind<G>(G obj)
            where G : class, T1
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueFunc<T2, T3, R>(methodPtr, obj);
                case Func<T1, T2, T3, R> func when methodPtr == IntPtr.Zero:
                    return new ValueFunc<T2, T3, R>(func.Bind<G, T2, T3, R>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
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
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Tail();
            Callvirt(new M(typeof(Func<T1, T2, T3, R>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T1), typeof(T2), typeof(T3)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, R>(ValueFunc<T1, T2, T3, R> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<T1, T2, T3, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueFunc<T1, T2, T3, R> first, ValueFunc<T1, T2, T3, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueFunc<T1, T2, T3, R> first, ValueFunc<T1, T2, T3, R> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with three parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueAction<T1, T2, T3> : IMethodPointer<Action<T1, T2, T3>>, IEquatable<ValueAction<T1, T2, T3>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T1, T2, T3>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="action">The delegate representing method.</param>
        public ValueAction(Action<T1, T2, T3> action)
        {
            if(action.Method.IsAbstract)
            {
                target = action;
                methodPtr = default;
            }
            else
            {
                target = action.Target;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueAction(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueAction<T1, T1, T3>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Action<T1, T2, T3>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2,T3}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Action<T1, T2, T3>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3>), typeof(object), typeof(IntPtr)));
            return Return<Action<T1, T2, T3>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueAction<G, T1, T2, T3> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueAction<G, T1, T2, T3>(methodPtr, null);
            if(target is Action<T1, T2, T3> action && methodPtr == IntPtr.Zero)
                return new ValueAction<G, T1, T2, T3>(action.Unbind<G, T1, T2, T3>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueAction<T2, T3> Bind<G>(G obj)
            where G : class, T1
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueAction<T2, T3>(methodPtr, obj);
                case Action<T1, T2, T3> action when methodPtr == IntPtr.Zero:
                    return new ValueAction<T2, T3>(action.Bind<G, T2, T3>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
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
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Tail();
            Callvirt(new M(typeof(Action<T1, T2, T3>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T1), typeof(T2), typeof(T3)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3>(ValueAction<T1, T2, T3> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueAction<T1, T2, T3> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueAction<T1, T2, T3> first, ValueAction<T1, T2, T3> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueAction<T1, T2, T3> first, ValueAction<T1, T2, T3> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with four parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueFunc<T1, T2, T3, T4, R> : IMethodPointer<Func<T1, T2, T3, T4, R>>, IEquatable<ValueFunc<T1, T2, T3, T4, R>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T1, T2, T3, T4, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="func">The delegate representing method.</param>
        public ValueFunc(Func<T1, T2, T3, T4, R> func)
        {
            if(func.Method.IsAbstract)
            {
                target = func;
                methodPtr = default;
            }
            else
            {
                target = func.Target;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueFunc(IntPtr methodPtr, object target)
        {
            this.target = target;
            this.methodPtr = methodPtr;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueFunc<T1, T1, T3, T4, R>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Func<T1, T2, T3, T4, R>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Func<T1, T2, T3, T4, R>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, T4, R>), typeof(object), typeof(IntPtr)));
            return Return<Func<T1, T2, T3, T4, R>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueFunc<G, T1, T2, T3, T4, R> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueFunc<G, T1, T2, T3, T4, R>(methodPtr, null);
            if(target is Func<T1, T2, T3, T4, R> func && methodPtr == IntPtr.Zero)
                return new ValueFunc<G, T1, T2, T3, T4, R>(func.Unbind<G, T1, T2, T3, T4, R>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueFunc<T2, T3, T4, R> Bind<G>(G obj)
            where G : class, T1
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueFunc<T2, T3, T4, R>(methodPtr, obj);
                case Func<T1, T2, T3, T4, R> func when methodPtr == IntPtr.Zero:
                    return new ValueFunc<T2, T3, T4, R>(func.Bind<G, T2, T3, T4, R>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
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
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Tail();
            Callvirt(new M(typeof(Func<T1, T2, T3, T4, R>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, T4, R>(ValueFunc<T1, T2, T3, T4, R> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<T1, T2, T3, T4, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueFunc<T1, T2, T3, T4, R> first, ValueFunc<T1, T2, T3, T4, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueFunc<T1, T2, T3, T4, R> first, ValueFunc<T1, T2, T3, T4, R> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with fourth parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueAction<T1, T2, T3, T4> : IMethodPointer<Action<T1, T2, T3, T4>>, IEquatable<ValueAction<T1, T2, T3, T4>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T1, T2, T3, T4>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="action">The delegate representing method.</param>
        public ValueAction(Action<T1, T2, T3, T4> action)
        {
            if(action.Method.IsAbstract)
            {
                target = action;
                methodPtr = default;
            }
            else
            {
                target = action.Target;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }
        
        internal ValueAction(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueAction<T1, T1, T3, T4>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Action<T1, T2, T3, T4>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1,T2,T3,T4}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Action<T1, T2, T3, T4>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3, T4>), typeof(object), typeof(IntPtr)));
            return Return<Action<T1, T2, T3, T4>>();
        }

        /// <summary>
        /// Converts implicitly bound method pointer into its unbound version.
        /// </summary>
        /// <typeparam name="G">The expected type of <see cref="Target"/>.</typeparam>
        /// <returns>Unbound version of method pointer.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Target"/> is not of type <typeparamref name="G"/>.</exception>
        public ValueAction<G, T1, T2, T3, T4> Unbind<G>()
            where G : class
        {
            if(ObjectExtensions.IsContravariant(target, typeof(G)))
                return new ValueAction<G, T1, T2, T3, T4>(methodPtr, null);
            if(target is Action<T1, T2, T3, T4> action && methodPtr == IntPtr.Zero)
                return new ValueAction<G, T1, T2, T3, T4>(action.Unbind<G, T1, T2, T3, T4>());
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueAction<T2, T3, T4> Bind<G>(G obj)
            where G : class, T1
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueAction<T2, T3, T4>(methodPtr, obj);
                case Action<T1, T2, T3, T4> action when methodPtr == IntPtr.Zero:
                    return new ValueAction<T2, T3, T4>(action.Bind<G, T2, T3, T4>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
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
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Tail();
            Callvirt(new M(typeof(Action<T1, T2, T3, T4>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3, T4>(ValueAction<T1, T2, T3, T4> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueAction<T1, T2, T3, T4> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueAction<T1, T2, T3, T4> first, ValueAction<T1, T2, T3, T4> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueAction<T1, T2, T3, T4> first, ValueAction<T1, T2, T3, T4> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with five parameters and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueFunc<T1, T2, T3, T4, T5, R> : IMethodPointer<Func<T1, T2, T3, T4, T5, R>>, IEquatable<ValueFunc<T1, T2, T3, T4, T5, R>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueFunc(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Func<T1, T2, T3, T4, T5, R>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="func">The delegate representing method.</param>
        public ValueFunc(Func<T1, T2, T3, T4, T5, R> func)
        {
            if(func.Method.IsAbstract)
            {
                target = func;
                methodPtr = default;
            }
            else
            {
                target = func.Target;
                methodPtr = func.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueFunc(IntPtr methodPtr, object target)
        {
            this.target = target;
            this.methodPtr = methodPtr;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueFunc<T1, T1, T3, T4, T5, R>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Func<T1, T2, T3, T4, T5, R>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<T1, T2, T3, T4, T5, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Func<T1, T2, T3, T4, T5, R>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Func<T1, T2, T3, T4, T5, R>), typeof(object), typeof(IntPtr)));
            return Return<Func<T1, T2, T3, T4, T5, R>>();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueFunc<T2, T3, T4, T5, R> Bind<G>(G obj)
            where G : class, T1
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueFunc<T2, T3, T4, T5, R>(methodPtr, obj);
                case Func<T1, T2, T3, T4, T5, R> func when methodPtr == IntPtr.Zero:
                    return new ValueFunc<T2, T3, T4, T5, R>(func.Bind<G, T2, T3, T4, T5, R>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
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
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Tail();
            Callvirt(new M(typeof(Func<T1, T2, T3, T4, T5, R>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(R), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{T1, T2, T3, T4, T5, TResult}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<T1, T2, T3, T4, T5, R>(ValueFunc<T1, T2, T3, T4, T5, R> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueFunc<T1, T2, T3, T4, T5, R> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueFunc<T1, T2, T3, T4, T5, R> first, ValueFunc<T1, T2, T3, T4, T5, R> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueFunc<T1, T2, T3, T4, T5, R> first, ValueFunc<T1, T2, T3, T4, T5, R> second) => !first.Equals(second);
    }

    /// <summary>
    /// Represents a pointer to the method with fifth parameters and <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// The method pointer supports the same semantics as regular .NET delegates.
    /// It means that pointer can be open (for instance methods) or closed (for static methods with captured first argument).
    /// </remarks>
    /// <typeparam name="T1">The type of the first method parameter.</typeparam>
    /// <typeparam name="T2">The type of the second method parameter.</typeparam>
    /// <typeparam name="T3">The type of the third method parameter.</typeparam>
    /// <typeparam name="T4">The type of the fourth method parameter.</typeparam>
    /// <typeparam name="T5">The type of the fifth method parameter.</typeparam>
    /// <seealso cref="Reflection.MethodCookie{D,P}"/>
    /// <seealso cref="Reflection.MethodCookie{T,D,P}"/>
    public readonly struct ValueAction<T1, T2, T3, T4, T5> : IMethodPointer<Action<T1, T2, T3, T4, T5>>, IEquatable<ValueAction<T1, T2, T3, T4, T5>>, IMethodCookieSupport
    {
        private readonly IntPtr methodPtr;
        private readonly object target;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <remarks>
        /// This constructor causes heap allocations because Reflection is needed to check compatibility of method's signature
        /// with the pointer type. To avoid these allocations, use <see cref="Reflection.MethodCookie{D,P}"/> or <see cref="Reflection.MethodCookie{T,D,P}"/> type.
        /// </remarks>
        /// <param name="method">The method to convert into pointer.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type.</exception>
        public ValueAction(MethodInfo method, object target = null)
            : this(method.CreateDelegate<Action<T1, T2, T3, T4, T5>>(target))
        {
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="action">The delegate representing method.</param>
        public ValueAction(Action<T1, T2, T3, T4, T5> action)
        {
            if(action.Method.IsAbstract)
            {
                target = action;
                methodPtr = default;
            }
            else
            {
                target = action.Target;
                methodPtr = action.Method.MethodHandle.GetFunctionPointer();
            }
        }

        internal ValueAction(IntPtr methodPtr, object target)
        {
            this.methodPtr = methodPtr;
            this.target = target;
        }

        void IMethodCookieSupport.Construct(IntPtr methodPtr, object target)
            => Jmp(M.Constructor(typeof(ValueAction<T1, T1, T3, T1, T5>), typeof(IntPtr), typeof(object)));

        IntPtr IMethodPointer<Action<T1, T2, T3, T4, T5>>.Address => methodPtr;

        /// <summary>
        /// Gets the object on which the current pointer invokes the method.
        /// </summary>
        public object Target => target;

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T1, T2, T3, T4, T5> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(target);
            Push(methodPtr);
            Dup();
            Brtrue(makeDelegate);
            Pop();
            Isinst(typeof(Action<T1, T2, T3, T4, T5>));
            Ret();
            MarkLabel(makeDelegate);
            Newobj(M.Constructor(typeof(Action<T1, T2, T3, T4, T5>), typeof(object), typeof(IntPtr)));
            return Return<Action<T1, T2, T3, T4, T5>>();
        }

        /// <summary>
        /// Produces method pointer which first argument is implicitly bound to the given object.
        /// </summary>
        /// <typeparam name="G">The type of the first argument to bind.</typeparam>
        /// <param name="obj">The object to be passed as first argument into the method represented by this pointer. Cannot be <see langword="null"/>.</param>
        /// <returns>The pointer to the method targeting the specified object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">This pointer has already bound to the object.</exception>
        public ValueAction<T2, T3, T4, T5> Bind<G>(G obj)
            where G : class, T1
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            switch(target)
            {
                case null:
                    return new ValueAction<T2, T3, T4, T5>(methodPtr, obj);
                case Action<T1, T2, T3, T4, T5> action when methodPtr == IntPtr.Zero:
                    return new ValueAction<T2, T3, T4, T5>(action.Bind<G, T2, T3, T4, T5>(obj, true));
                default:
                    throw new InvalidOperationException();
            }
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
            Push(target);
            Push(methodPtr);
            Brtrue(callIndirect);
            Push(arg1);
            Push(arg2);
            Push(arg3);
            Push(arg4);
            Push(arg5);
            Tail();
            Callvirt(new M(typeof(Action<T1, T2, T3, T4, T5>), nameof(Invoke)));
            Ret();
            MarkLabel(callIndirect);
            
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
            Tail();
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <param name="pointer">The pointer to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T1, T2, T3, T4, T5>(ValueAction<T1, T2, T3, T4, T5> pointer) => pointer.ToDelegate();

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
        public bool Equals(ValueAction<T1, T2, T3, T4, T5> other) => methodPtr == other.methodPtr && ReferenceEquals(target, other.target);

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
        public override string ToString() => $"Address={methodPtr:X}, Target={target}";

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ValueAction<T1, T2, T3, T4, T5> first, ValueAction<T1, T2, T3, T4, T5> second) => first.Equals(second);

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ValueAction<T1, T2, T3, T4, T5> first, ValueAction<T1, T2, T3, T4, T5> second) => !first.Equals(second);
    }
}
