using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;

namespace DotNext.Reflection
{
    using RuntimeFeaturesAttribute = Runtime.CompilerServices.RuntimeFeaturesAttribute;

    [RuntimeFeatures(PrivateReflection = true)]
    internal readonly struct MethodPointerFactory<P>
        where P : struct, IMethodPointer<Delegate>
    {
        private static readonly RuntimeMethodHandle ConstructorHandle;

        static MethodPointerFactory()
        {
            //FIXME: It is bad trick based on assumption that every method pointer has appropriate constructor. It would be better to have another way (i.e. under compiler control) of generic instantation of method pointers but I don't see it at this moment
            var ctor = typeof(P).GetConstructor(BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance, null, new[] { typeof(RuntimeMethodHandle), typeof(bool) }, Array.Empty<ParameterModifier>()) ?? throw new GenericArgumentException<P>(ExceptionMessages.UnsupportedMethodPointerType);
            ConstructorHandle = ctor.MethodHandle;
        }

        private readonly RuntimeMethodHandle method;
        private readonly IntPtr ctorPtr;    //pointer to constructor P::.ctor(RuntimeMethodHandle, object)

        internal MethodPointerFactory(MethodInfo method)
        {
            this.method = method.MethodHandle;
            ctorPtr = ConstructorHandle.GetFunctionPointer();
        }

        internal MethodBase Method => MethodBase.GetMethodFromHandle(method);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal P Create(object target = null)
        {
            var result = default(P);
            const string MethodExit = "exit";
            Push(ctorPtr);
            Brfalse(MethodExit);
            
            Push(ref result);   //this arg
            Push(method);
            Push(target);
            Push(ctorPtr);
            //here I use constructor as instance method to initialize pointer of type P. As far as I know it is legal because constructor is
            //indistinguishable from regular instance method at CLR level. Anyway, it is cheaper that doing Reflection call with all these boxing/unboxing
            Calli(new CallSiteDescr(CallingConventions.HasThis, typeof(void), typeof(RuntimeMethodHandle), typeof(object)));
            MarkLabel(MethodExit);
            return result;
        }
    }

    /// <summary>
    /// Represents a source of static method pointers.
    /// </summary>
    /// <remarks>
    /// The reason to having this type is to avoid heap allocations every time
    /// when you need typed method pointer. The constructor of such pointer
    /// performs runtime checks using Reflection. These checks require such allocations.
    /// To avoid that, it is possible to create <see cref="MethodCookie{D, P}"/>
    /// once and store it in <c>static readonly</c> field. After that, every creation
    /// of typed method pointer doesn't produce unecessary memory allocations.
    /// </remarks>
    /// <typeparam name="P">The type of the managed pointer.</typeparam>
    /// <typeparam name="D">The type of the delegate compatible with managed pointer.</typeparam>
    public readonly struct MethodCookie<D, P>
        where D : Delegate
        where P : struct, IMethodPointer<D>
    {
        private readonly MethodPointerFactory<P> factory;

        /// <summary>
        /// Initializes a new static method cookie.
        /// </summary>
        /// <param name="delegate">The delegate referencing a static method for which pointers should be created.</param>
        public MethodCookie(D @delegate)
            => factory = new MethodPointerFactory<P>(@delegate.Target is null ? @delegate.Method : throw new ArgumentException(ExceptionMessages.InvalidMethodSignature, nameof(@delegate)));

        /// <summary>
        /// Initializes a new static method cookie.
        /// </summary>
        /// <param name="method">A static method for which pointers should be created.</param>
        public MethodCookie(MethodInfo method)
            : this(method.CreateDelegate<D>())
        {
        }

        /// <summary>
        /// Gets pointer to the underlying static method.
        /// </summary>
        /// <value>The pointer to the underlying static method.</value>
        public P Pointer => factory.Create();

        /// <summary>
        /// Gets underlying method in textual format.
        /// </summary>
        /// <returns>The method represented by this cookie in the form of string.</returns>
        public override string ToString() => factory.Method.ToString();

        /// <summary>
        /// Gets pointer to the underlying static method.
        /// </summary>
        /// <param name="cookie">A cookie representing validated method.</param>
        /// <returns>The pointer to the underlying static method.</returns>
        public static implicit operator P(in MethodCookie<D, P> cookie) => cookie.Pointer;
    }

    /// <summary>
    /// Represents a source of instance method pointers.
    /// </summary>
    /// <remarks>
    /// The reason to having this type is to avoid heap allocations every time
    /// when you need typed method pointer. The constructor of such pointer
    /// performs runtime checks using Reflection. These checks require such allocations.
    /// To avoid that, it is possible to create <see cref="MethodCookie{T,D,P}"/>
    /// once and store it in <c>static readonly</c> field. After that, every creation
    /// of typed method pointer doesn't produce unecessary memory allocations.
    /// </remarks>
    /// <typeparam name="T">The type of the method target.</typeparam>
    /// <typeparam name="D">The type of the delegate compatible with managed pointer.</typeparam>
    /// <typeparam name="P">The type of the managed pointer.</typeparam>
    public readonly struct MethodCookie<T, D, P>
        where D : Delegate
        where T : class
        where P : struct, IMethodPointer<D>
    {
        private readonly MethodPointerFactory<P> factory;

        /// <summary>
        /// Initializes a new instance method cookie.
        /// </summary>
        /// <param name="delegate">The delegate referencing an instance method for which pointers should be created.</param>
        public MethodCookie(D @delegate)
            => factory = new MethodPointerFactory<P>( @delegate.Target is T ? @delegate.Method : throw new ArgumentException(ExceptionMessages.InvalidMethodSignature, nameof(@delegate)));

        /// <summary>
        /// Initializes a new instance method cookie.
        /// </summary>
        /// <param name="method">An instance method for which pointers should be created.</param>
        public MethodCookie(MethodInfo method)
        {
            Type[] expectedParams;
            Type expectedReturnType;
            {
                var invokeMethod = DelegateType.GetInvokeMethod<D>();
                expectedParams = invokeMethod.GetParameterTypes();
                expectedReturnType = invokeMethod.ReturnType;
            }
            bool thisTypeOk;
            if(method.IsStatic)
            {
                expectedParams = expectedParams.Insert(typeof(T), 0L);
                thisTypeOk = true;
            }
            else
                thisTypeOk = method.DeclaringType.IsAssignableFrom(typeof(T));
            if(thisTypeOk && method.SignatureEquals(expectedParams) && expectedReturnType.IsAssignableFrom(method.ReturnType))
                factory = new MethodPointerFactory<P>(method);
            else
                throw new ArgumentException(ExceptionMessages.InvalidMethodSignature, nameof(method));
        }

        /// <summary>
        /// Creates pointer to the instance method.
        /// </summary>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The created pointer.</returns>
        public P CreatePointer(T obj) => factory.Create(obj ?? throw new ArgumentNullException(nameof(obj)));

        /// <summary>
        /// Gets underlying method in textual format.
        /// </summary>
        /// <returns>The method represented by this cookie in the form of string.</returns>
        public override string ToString() => factory.Method.ToString();

        /// <summary>
        /// Creates pointer to the instance method.
        /// </summary>
        /// <param name="cookie">A cookie representing validated method.</param>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The created pointer.</returns>
        public static P operator &(in MethodCookie<T, D, P> cookie, T obj) => cookie.CreatePointer(obj); 
    }
}