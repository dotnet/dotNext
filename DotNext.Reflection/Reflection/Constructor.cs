using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cheats.Reflection
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

        public static implicit operator D(Constructor<D> ctor) => ctor?.invoker;

        public override string Name => ctor?.Name ?? ".ctor";

        ConstructorInfo IMember<ConstructorInfo>.RuntimeMember => ctor;

        D ICallable<D>.Invoker => invoker;

        public override MethodAttributes Attributes => ctor is null ? (MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName) : ctor.Attributes;

        public override RuntimeMethodHandle MethodHandle => ctor is null ? invoker.Method.MethodHandle : ctor.MethodHandle;

        public override Type DeclaringType { get; }

        public override Type ReflectedType => ctor is null ? ctor.ReflectedType : invoker.Method.ReflectedType;

        public override CallingConventions CallingConvention => ctor is null ? invoker.Method.CallingConvention : ctor.CallingConvention;

        public override bool ContainsGenericParameters => false;

        public override IEnumerable<CustomAttributeData> CustomAttributes => GetCustomAttributesData();

        public override MethodBody GetMethodBody() => ctor?.GetMethodBody() ?? invoker.Method.GetMethodBody();

        public override IList<CustomAttributeData> GetCustomAttributesData() => ctor?.GetCustomAttributesData() ?? Array.Empty<CustomAttributeData>();

        public override Type[] GetGenericArguments() => Array.Empty<Type>();

        public override bool IsGenericMethod => false;

        public override bool IsGenericMethodDefinition => false;

        public override bool IsSecurityCritical => ctor is null ? invoker.Method.IsSecurityCritical : ctor.IsSecurityCritical;

        public override bool IsSecuritySafeCritical => ctor is null ? invoker.Method.IsSecuritySafeCritical : ctor.IsSecuritySafeCritical;

        public override bool IsSecurityTransparent => ctor is null ? invoker.Method.IsSecurityTransparent : ctor.IsSecurityTransparent;

        public override MemberTypes MemberType => MemberTypes.Constructor;

        public override int MetadataToken => ctor is null ? invoker.Method.MetadataToken : ctor.MetadataToken;

        public override MethodImplAttributes MethodImplementationFlags => ctor is null ? invoker.Method.MethodImplementationFlags : ctor.MethodImplementationFlags;

        public override Module Module => ctor is null ? DeclaringType.Module : ctor.Module;

        public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            => Invoke(null, invokeAttr, binder, parameters, culture);

        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;

        public override ParameterInfo[] GetParameters() => ctor is null ? invoker.Method.GetParameters() : ctor.GetParameters();

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
            ;
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
				var (parameters, returnType) = DelegateCheats.GetInvokeMethod<D>().Decompose(MethodCheats.GetParameterTypes, method => method.ReturnType);
				return Reflect(returnType, parameters, nonPublic);
			}            
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
			{
				var (parameters, arglist, input) = Signature.Reflect(argumentsType);
				return returnType.IsAssignableFrom(ctor.DeclaringType) && ctor.SignatureEquals(parameters) ? new Constructor<D>(ctor, arglist, new[] { input }) : null;
			}
			else
			{
				var invokeMethod = DelegateCheats.GetInvokeMethod<D>();
				return ctor.SignatureEquals(invokeMethod) && invokeMethod.ReturnType.IsAssignableFrom(ctor.DeclaringType) ?
					new Constructor<D>(ctor, ctor.GetParameterTypes().Convert(Expression.Parameter)) :
					null;
			}
        }
    }
}