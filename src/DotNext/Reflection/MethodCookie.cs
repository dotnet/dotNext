using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Runtime.Serialization.FormatterServices;

namespace DotNext.Reflection
{
    using RuntimeFeaturesAttribute = Runtime.CompilerServices.RuntimeFeaturesAttribute;

    /// <summary>
    /// Represents interface for value delegates supporting instantiation
    /// through <see cref="MethodCookie{D,P}"/> or <see cref="MethodCookie{T,D,P}"/>.
    /// </summary>
    /// <remarks>
    /// This API supports the product infrastructure and is not intended to be used directly from your code. 
    /// </remarks>
    public interface IMethodCookieSupport
    {
        /// <summary>
        /// Initializes value delegate from the surrogate of the method pointer.
        /// </summary>
        /// <param name="methodPtr">The pointer to the managed method.</param>
        /// <param name="target">The object targeted by the method pointer.</param>
        void Construct(IntPtr methodPtr, object target);
    }

    [RuntimeFeatures(PrivateReflection = true)]
    internal readonly struct MethodPointerFactory<P>
        where P : struct, IMethodPointer<Delegate>, IMethodCookieSupport
    {
        private readonly RuntimeMethodHandle method;

        internal MethodPointerFactory(MethodInfo method)
        {
            this.method = method.MethodHandle;
        }

        internal MethodBase Method => MethodBase.GetMethodFromHandle(method);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal P Create(object target = null)
        {
            var result = default(P);
            if(target is Delegate && ValueType<RuntimeMethodHandle>.IsDefault(method))
                result.Construct(IntPtr.Zero, target);
            else
                result.Construct(method.GetFunctionPointer(), target);
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
        where P : struct, IMethodPointer<D>, IMethodCookieSupport
    {
        private readonly MethodPointerFactory<P> factory;
        private readonly D prototype;

        /// <summary>
        /// Initializes a new static method cookie.
        /// </summary>
        /// <param name="delegate">The delegate referencing a static method for which pointers should be created.</param>
        public MethodCookie(D @delegate)
        {
            if(@delegate.Target != null)
                throw new ArgumentException(ExceptionMessages.InvalidMethodSignature, nameof(@delegate));
            if(@delegate.Method.IsAbstract)
            {
                factory = default;
                prototype = @delegate;
            }
            else
            {
                factory = new MethodPointerFactory<P>(@delegate.Method);
                prototype = null;
            }
        }

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
        public P Pointer => factory.Create(prototype);

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
        where P : struct, IMethodPointer<D>, IMethodCookieSupport
    {
        private readonly MethodPointerFactory<P> factory;
        private readonly MethodInfo prototype;

        /// <summary>
        /// Initializes a new instance of method cookie.
        /// </summary>
        /// <param name="delegate">The delegate referencing an instance method for which pointers should be created.</param>
        public MethodCookie(D @delegate)
        {
            if(!(@delegate.Target is T))
                throw new ArgumentException(ExceptionMessages.InvalidMethodSignature, nameof(@delegate));
            var method = typeof(T).Devirtualize(@delegate.Method);
            if(method is null)
            {
                prototype = @delegate.Method;
                factory = default;
            }
            else
            {
                prototype = null;
                factory = new MethodPointerFactory<P>(method);
            }
        }

        /// <summary>
        /// Initializes a new instance of method cookie.
        /// </summary>
        /// <remarks>
        /// This constructor is supported only when <typeparamref name="T"/> is not an interface.
        /// </remarks>
        /// <param name="method">An instance method for which pointers should be created.</param>
        /// <exception cref="ArgumentException">The method is abstract.</exception>
        public MethodCookie(MethodInfo method)
            : this(method.CreateDelegate<D>(CreateStub()))
        {
        }

        [SuppressMessage("Usage", "CA1816", Justification = "SuppressFinalize is required to avoid possible problems when finalizing uninitialized object")]
        private static object CreateStub()
        {
            if (typeof(T) == typeof(string))
                return string.Empty;
            //TODO: Should be replaced with RuntimeHelpers.GetUninitializedObject in .NET Standard 2.1
            var obj = GetSafeUninitializedObject(typeof(T));
            GC.SuppressFinalize(obj);
            return obj;
        }

        /// <summary>
        /// Creates value delegate targeting the specified object.
        /// </summary>
        /// <param name="obj">The object to be used as <c>this</c> argument.</param>
        /// <returns>The created pointer.</returns>
        public P Bind(T obj)
        {
            if(obj is null)
                throw new ArgumentNullException(nameof(obj));
            return prototype is null ? 
                factory.Create(obj) :
                factory.Create(prototype.CreateDelegate<D>(obj));
        }

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
        public static P operator &(in MethodCookie<T, D, P> cookie, T obj) => cookie.Bind(obj); 
    }
}