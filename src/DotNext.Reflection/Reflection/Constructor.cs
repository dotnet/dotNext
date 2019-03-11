using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides constructor definition based on delegate signature.
    /// </summary>
    /// <typeparam name="D">Type of delegate representing constructor of type <typeparamref name="D"/>.</typeparam>
    public sealed class Constructor<D> : ConstructorInfo, IConstructor<D>, IEquatable<ConstructorInfo>, IEquatable<Constructor<D>>
        where D : MulticastDelegate
    {
        private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.NonPublic;

        private readonly D invoker;
        private readonly ConstructorInfo ctor;

		private Constructor(ConstructorInfo ctor, Expression<D> invoker)
		{
			this.ctor = ctor;
			this.invoker = invoker.Compile();
		}

        private Constructor(ConstructorInfo ctor, Expression[] args, ParameterExpression[] parameters)
        {
            DeclaringType = ctor.DeclaringType;
            this.ctor = ctor;
            invoker = Expression.Lambda<D>(Expression.New(ctor, args), parameters).Compile();
        }

        private Constructor(ConstructorInfo ctor, ParameterExpression[] parameters)
            : this(ctor, parameters, parameters)
        {
        }

        private Constructor(Type valueType, ParameterExpression[] parameters)
        {
            DeclaringType = valueType;
            invoker = Expression.Lambda<D>(Expression.Default(valueType), parameters).Compile();
        }

        internal Constructor<D> OfType<T>() => DeclaringType == typeof(T) ? this : null;

        /// <summary>
        /// Extracts delegate which can be used to invoke this constructor.
        /// </summary>
        /// <param name="ctor">The reflected constructor.</param>
        public static implicit operator D(Constructor<D> ctor) => ctor?.invoker;

        /// <summary>
        /// Gets name of the constructor.
        /// </summary>
        public override string Name => ctor?.Name ?? ".ctor";

        ConstructorInfo IMember<ConstructorInfo>.RuntimeMember => ctor;

        D IMember<ConstructorInfo, D>.Invoker => invoker;

        /// <summary>
        /// Gets the attributes associated with this constructor.
        /// </summary>
        public override MethodAttributes Attributes => ctor is null ? (MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName) : ctor.Attributes;

        /// <summary>
        /// Gets a handle to the internal metadata representation of a constructor.
        /// </summary>
        public override RuntimeMethodHandle MethodHandle => ctor is null ? invoker.Method.MethodHandle : ctor.MethodHandle;

        /// <summary>
        /// Gets the class that declares this member.
        /// </summary>
        public override Type DeclaringType { get; }

        /// <summary>
        /// Gets the class object that was used to obtain this instance.
        /// </summary>
        public override Type ReflectedType => ctor is null ? ctor.ReflectedType : invoker.Method.ReflectedType;

        /// <summary>
        /// Gets a value indicating the calling conventions for this constructor.
        /// </summary>
        public override CallingConventions CallingConvention => ctor is null ? invoker.Method.CallingConvention : ctor.CallingConvention;

        /// <summary>
        /// Gets a value indicating whether the generic method contains unassigned generic type parameters.
        /// </summary>
        public override bool ContainsGenericParameters => false;

        /// <summary>
        /// Gets a collection that contains this member's custom attributes.
        /// </summary>
        public override IEnumerable<CustomAttributeData> CustomAttributes => GetCustomAttributesData();

        /// <summary>
        /// When overridden in a derived class, gets a System.Reflection.MethodBody object that provides access to the MSIL stream, 
        /// local variables, and exceptions for the current method.
        /// </summary>
        /// <returns>An object that provides access to the MSIL stream, local variables, and exceptions for the current method.</returns>
        public override MethodBody GetMethodBody() => ctor?.GetMethodBody() ?? invoker.Method.GetMethodBody();

        /// <summary>
        /// Returns a list of custom attributes that have been applied to the target member.
        /// </summary>
        /// <returns>The data about the attributes that have been applied to the target member.</returns>
        public override IList<CustomAttributeData> GetCustomAttributesData() => ctor?.GetCustomAttributesData() ?? Array.Empty<CustomAttributeData>();

        /// <summary>
        /// Returns the type arguments of a generic method or the type parameters of a generic method definition.
        /// </summary>
        /// <returns>The list of generic arguments.</returns>
        public override Type[] GetGenericArguments() => Array.Empty<Type>();

        /// <summary>
        /// Gets a value indicating whether the constructor is generic.
        /// </summary>
        public override bool IsGenericMethod => false;

        /// <summary>
        /// Gets a value indicating whether the constructor is a generic method definition.
        /// </summary>
        public override bool IsGenericMethodDefinition => false;

        /// <summary>
        /// Gets a value that indicates whether the constructor is security-critical or security-safe-critical at the current trust level, 
        /// and therefore can perform critical operations.
        /// </summary>
        public override bool IsSecurityCritical => ctor is null ? invoker.Method.IsSecurityCritical : ctor.IsSecurityCritical;

        /// <summary>
        /// Gets a value that indicates whether the constructor is security-safe-critical at the current trust level; that is, 
        /// whether it can perform critical operations and can be accessed by transparent code.
        /// </summary>
        public override bool IsSecuritySafeCritical => ctor is null ? invoker.Method.IsSecuritySafeCritical : ctor.IsSecuritySafeCritical;

        /// <summary>
        /// Gets a value that indicates whether the current method or constructor is transparent at the current trust level, 
        /// and therefore cannot perform critical operations.
        /// </summary>
        public override bool IsSecurityTransparent => ctor is null ? invoker.Method.IsSecurityTransparent : ctor.IsSecurityTransparent;

        /// <summary>
        /// Always returns <see cref="MemberTypes.Constructor"/>.
        /// </summary>
        public override MemberTypes MemberType => MemberTypes.Constructor;

        /// <summary>
        /// Gets a value that identifies a metadata element.
        /// </summary>
        public override int MetadataToken => ctor is null ? invoker.Method.MetadataToken : ctor.MetadataToken;

        /// <summary>
        /// Gets constructor implementation attributes.
        /// </summary>
        public override MethodImplAttributes MethodImplementationFlags => ctor is null ? invoker.Method.MethodImplementationFlags : ctor.MethodImplementationFlags;

        /// <summary>
        /// Gets the module in which the type that declares the constructor represented by the current instance is defined.
        /// </summary>
        public override Module Module => ctor is null ? DeclaringType.Module : ctor.Module;

        /// <summary>
        /// Invokes this constructor.
        /// </summary>
        /// <param name="invokeAttr">Specifies the type of binding.</param>
        /// <param name="binder">Defines a set of properties and enables the binding, coercion of argument types, and invocation of members using reflection</param>
        /// <param name="parameters">A list of constructor arguments.</param>
        /// <param name="culture">Used to govern the coercion of types.</param>
        /// <returns>Instantiated object.</returns>
        public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            => Invoke(null, invokeAttr, binder, parameters, culture);

        /// <summary>
        /// Gets constructor implementation attributes.
        /// </summary>
        /// <returns>Implementation attributes.</returns>
        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;

        /// <summary>
        /// Gets constructor parameters.
        /// </summary>
        /// <returns>The array of constructor parameters.</returns>
        public override ParameterInfo[] GetParameters() => ctor is null ? invoker.Method.GetParameters() : ctor.GetParameters();

        /// <summary>
        /// Invokes this constructor.
        /// </summary>
        /// <param name="obj">The object on which to invoke the constructor.</param>
        /// <param name="invokeAttr">Specifies the type of binding.</param>
        /// <param name="binder">Defines a set of properties and enables the binding, coercion of argument types, and invocation of members using reflection</param>
        /// <param name="parameters">A list of constructor arguments.</param>
        /// <param name="culture">Used to govern the coercion of types.</param>
        /// <returns>Instantiated object.</returns>
        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            => ctor is null ? invoker.Method.Invoke(obj, invokeAttr, binder, parameters, culture) : ctor.Invoke(obj, invokeAttr, binder, parameters, culture);

        public override object[] GetCustomAttributes(bool inherit)
            => ctor is null ? Array.Empty<object>() : ctor.GetCustomAttributes(inherit);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            => ctor is null ? Array.Empty<object>() : ctor.GetCustomAttributes(attributeType, inherit);

        public override bool IsDefined(Type attributeType, bool inherit)
            => ctor is null ? false : ctor.IsDefined(attributeType, inherit);

        public bool Equals(ConstructorInfo other) => ctor == other;

        public bool Equals(Constructor<D> other) => ctor == other.ctor;

        public override bool Equals(object other)
        {
            switch (other)
            {
                case Constructor<D> ctor:
                    return Equals(ctor);
                case ConstructorInfo ctor:
                    return Equals(ctor);
                default:
                    return false;
            }
        }

        public override string ToString() => ctor is null ? invoker.ToString() : ctor.ToString();

        public override int GetHashCode() => ctor is null ? DeclaringType.GetHashCode() : ctor.GetHashCode();

        private static Constructor<D> Reflect(Type declaringType, Type[] parameters, bool nonPublic)
        {
            if (declaringType.IsValueType)
                return new Constructor<D>(declaringType, parameters.Convert(Expression.Parameter));
            else
            {
                var ctor = declaringType.GetConstructor(nonPublic ? NonPublicFlags : PublicFlags, Type.DefaultBinder, parameters, Array.Empty<ParameterModifier>());
                return ctor is null ? null : new Constructor<D>(ctor, parameters.Convert(Expression.Parameter));
            }
        }

        private static Constructor<D> Reflect(Type declaringType, Type argumentsType, bool nonPublic)
        {
            var (parameters, arglist, input) = Signature.Reflect(argumentsType);
            //handle value type
            if(declaringType.IsValueType)
                return new Constructor<D>(declaringType, parameters.Convert(Expression.Parameter));

            var ctor = declaringType.GetConstructor(nonPublic ? NonPublicFlags : PublicFlags, Type.DefaultBinder, parameters, Array.Empty<ParameterModifier>());
            return ctor is null ? null : new Constructor<D>(ctor, arglist, new[]{ input });
        }

        internal static Constructor<D> Reflect(bool nonPublic)
        {
            var delegateType = typeof(D);
			if (delegateType.IsGenericInstanceOf(typeof(Function<,>)) && typeof(D).GetGenericArguments().Take(out var argumentsType, out var declaringType) == 2L)
				return Reflect(declaringType, argumentsType, nonPublic);
			else if (delegateType.IsAbstract)
				throw new AbstractDelegateException<D>();
			else
			{
				var (parameters, returnType) = DelegateType.GetInvokeMethod<D>().Decompose(Method.GetParameterTypes, method => method.ReturnType);
				return Reflect(returnType, parameters, nonPublic);
			}            
        }

		private static Constructor<D> Unreflect(ConstructorInfo ctor, Type argumentsType, Type returnType)
		{
			var (_, arglist, input) = Signature.Reflect(argumentsType);
			var prologue = new LinkedList<Expression>();
			var epilogue = new LinkedList<Expression>();
			var locals = new LinkedList<ParameterExpression>();
			//adjust arguments
			if (!Signature.NormalizeArguments(ctor.GetParameterTypes(), arglist, locals, prologue, epilogue))
				return null;
			Expression body;
			//adjust return type
			if (returnType == typeof(void) || returnType.IsAssignableFromWithoutBoxing(ctor.DeclaringType))
				body = Expression.New(ctor, arglist);
			else if (returnType == typeof(object))
				body = Expression.Convert(Expression.New(ctor, arglist), returnType);
			else
				return null;
			if (epilogue.Count == 0)
				epilogue.AddFirst(body);
			else
			{
				var returnArg = Expression.Parameter(returnType);
				locals.AddFirst(returnArg);
				body = Expression.Assign(returnArg, body);
				epilogue.AddFirst(body);
				epilogue.AddLast(returnArg);
			}
			body = prologue.Count == 0 && epilogue.Count == 1 ? epilogue.First.Value : Expression.Block(locals, prologue.Concat(epilogue));
			return new Constructor<D>(ctor, Expression.Lambda<D>(body, input));
		}

		internal static Constructor<D> Unreflect(ConstructorInfo ctor)
        {
            var delegateType = typeof(D);
			if (delegateType.IsAbstract)
				throw new AbstractDelegateException<D>();
			else if (ctor is Constructor<D> existing)
				return existing;
			else if (ctor.IsGenericMethodDefinition || ctor.IsAbstract)
				return null;
			else if (delegateType.IsGenericInstanceOf(typeof(Function<,>)) && delegateType.GetGenericArguments().Take(out var argumentsType, out var returnType) == 2L)
				return Unreflect(ctor, argumentsType, returnType);
			else
			{
				var invokeMethod = DelegateType.GetInvokeMethod<D>();
				return ctor.SignatureEquals(invokeMethod) && invokeMethod.ReturnType.IsAssignableFrom(ctor.DeclaringType) ?
					new Constructor<D>(ctor, ctor.GetParameterTypes().Convert(Expression.Parameter)) :
					null;
			}
        }
    }
}