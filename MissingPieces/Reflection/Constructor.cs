using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace MissingPieces.Reflection
{
    using VariantType;

    /// <summary>
    /// Provides constructor definition based on delegate signature.
    /// </summary>
    /// <typeparam name="D">Type of delegate representing constructor of type <typeparamref name="D"/>.</typeparam>
    public sealed class Constructor<D> : ConstructorInfo, IConstructor<D>, IEquatable<ConstructorInfo>, IEquatable<Constructor<D>>
        where D : class, MulticastDelegate
    {
        private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.NonPublic;

        private readonly D invoker;
        private readonly Variant<ConstructorInfo, Type> ctorOrDeclaringType;

        private Constructor(ConstructorInfo ctor, Expression[] args, ParameterExpression[] parameters)
        {
            ctorOrDeclaringType = ctor;
            invoker = Expression.Lambda<D>(Expression.New(ctor, args), parameters).Compile();
        }

        private Constructor(ConstructorInfo ctor, ParameterExpression[] parameters)
            : this(ctor, parameters, parameters)
        {
        }

        private Constructor(Type valueType, ParameterExpression[] parameters)
        {
            ctorOrDeclaringType = valueType;
            invoker = Expression.Lambda<D>(Expression.Default(valueType), parameters).Compile();
        }

        internal Constructor<D> OfType<T>() => DeclaringType == typeof(T) ? this : null;

        public static implicit operator D(Constructor<D> ctor) => ctor?.invoker;

        public override string Name => ctorOrDeclaringType.First.OrDefault()?.Name ?? ".ctor";

        ConstructorInfo IMember<ConstructorInfo>.RuntimeMember => (ConstructorInfo)ctorOrDeclaringType;

        D ICallable<D>.Invoker => invoker;

        public override MethodAttributes Attributes => ctorOrDeclaringType.First.Map(ctor => ctor.Attributes).Or(invoker.Method.Attributes);

        public override RuntimeMethodHandle MethodHandle => ctorOrDeclaringType.First.Map(ctor => ctor.MethodHandle).Or(invoker.Method.MethodHandle);

        public override Type DeclaringType => ctorOrDeclaringType.UnifyFirst(ctor => ctor.DeclaringType);

        public override Type ReflectedType => ctorOrDeclaringType.First.Map(ctor => ctor.ReflectedType).Or(invoker.Method.ReflectedType);

        public override CallingConventions CallingConvention => ctorOrDeclaringType.First.Map(ctor => ctor.CallingConvention).Or(invoker.Method.CallingConvention);


        public override bool ContainsGenericParameters => false;

        public override IEnumerable<CustomAttributeData> CustomAttributes => GetCustomAttributesData();

        public override MethodBody GetMethodBody() => ctorOrDeclaringType.First.Map(ctor => ctor.GetMethodBody()).OrInvoke(invoker.Method.GetMethodBody);

        public override IList<CustomAttributeData> GetCustomAttributesData() => ctorOrDeclaringType.First.Map(ctor => ctor.GetCustomAttributesData()).OrInvoke(Array.Empty<CustomAttributeData>);

        public override Type[] GetGenericArguments() => Array.Empty<Type>();

        public override bool IsGenericMethod => false;

        public override bool IsGenericMethodDefinition => false;

        public override bool IsSecurityCritical => ctorOrDeclaringType.First.Map(ctor => ctor.IsSecurityCritical).Or(invoker.Method.IsSecurityCritical);

        public override bool IsSecuritySafeCritical => ctorOrDeclaringType.First.Map(ctor => ctor.IsSecuritySafeCritical).Or(invoker.Method.IsSecuritySafeCritical);

        public override bool IsSecurityTransparent => ctorOrDeclaringType.First.Map(ctor => ctor.IsSecurityTransparent).Or(invoker.Method.IsSecurityTransparent);

        public override MemberTypes MemberType => MemberTypes.Constructor;

        public override int MetadataToken => ctorOrDeclaringType.First.Map(ctor => ctor.MetadataToken).Or(invoker.Method.MetadataToken);

        public override MethodImplAttributes MethodImplementationFlags => ctorOrDeclaringType.First.Map(ctor => ctor.MethodImplementationFlags).Or(invoker.Method.MethodImplementationFlags);

        public override Module Module => ctorOrDeclaringType.UnifyFirst(ctor => ctor.DeclaringType).Module;

        public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            => Invoke(null, invokeAttr, binder, parameters, culture);

        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;

        public override ParameterInfo[] GetParameters() => ctorOrDeclaringType.First.Map(ctor => ctor.GetParameters()).OrInvoke(Array.Empty<ParameterInfo>);

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            var ctor = (ConstructorInfo)ctorOrDeclaringType;
            return ctor == null ? invoker.Method.Invoke(obj, invokeAttr, binder, parameters, culture) : ctor.Invoke(obj, invokeAttr, binder, parameters, culture);
        }

        public override object[] GetCustomAttributes(bool inherit)
            => ctorOrDeclaringType.First.Map(ctor => ctor.GetCustomAttributes(inherit)).OrInvoke(Array.Empty<object>);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            => ctorOrDeclaringType.First.Map(ctor => ctor.GetCustomAttributes(attributeType, inherit)).OrInvoke(Array.Empty<object>);

        public override bool IsDefined(Type attributeType, bool inherit)
            => ctorOrDeclaringType.First.Map(ctor => ctor.IsDefined(attributeType, inherit)).Or(false);

        public bool Equals(ConstructorInfo other) => ctorOrDeclaringType == other;

        public bool Equals(Constructor<D> other) => ctorOrDeclaringType == other.ctorOrDeclaringType;

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

        public override string ToString() => ctorOrDeclaringType.First.Map(ctor => ctor.ToString()).OrInvoke(invoker.Method.ToString);

        public override int GetHashCode() => ctorOrDeclaringType.GetHashCode();

        private static Constructor<D> ReflectSimple(bool nonPublic)
        {
            var (parameters, returnType) = Delegates.GetInvokeMethod<D>().Decompose(Methods.GetParameterTypes, method => method.ReturnType);
            if (returnType.IsValueType)
                return new Constructor<D>(returnType, parameters.Map(Expression.Parameter));
            else
            {
                var ctor = returnType.GetConstructor(nonPublic ? NonPublicFlags : PublicFlags, Type.DefaultBinder, parameters, Array.Empty<ParameterModifier>());
                return ctor is null ? null : new Constructor<D>(ctor, parameters.Map(Expression.Parameter));
            }
        }

        private static Constructor<D> ReflectSpecial(bool nonPublic)
        {
            typeof(D).GetGenericArguments().Take(out var argumentsType, out var declaringType);
            var (parameters, arglist, input) = Signature.Reflect(argumentsType);
            //handle value type
            if(declaringType.IsValueType)
                return new Constructor<D>(declaringType, parameters.Map(Expression.Parameter));

            var ctor = declaringType.GetConstructor(nonPublic ? NonPublicFlags : PublicFlags, Type.DefaultBinder, parameters, Array.Empty<ParameterModifier>());
            return ctor is null ? null : new Constructor<D>(ctor, arglist, new[]{ input });
        }

        internal static Constructor<D> Reflect(bool nonPublic)
        {
            var delegateType = typeof(D);
            if(delegateType.IsGenericInstanceOf(typeof(Function<,>)))
                return ReflectSpecial(nonPublic);
            else if(delegateType.IsAbstract)
                throw GenericArgumentException.Create<D>("Delegate type should not be abstract");
            return ReflectSimple(nonPublic);                
        }

        internal static Constructor<D> Reflect(ConstructorInfo ctor)
        {
            var delegateType = typeof(D);
            if(delegateType.IsAbstract)
                throw GenericArgumentException.Create<D>("Delegate type should not be abstract");
            else if(ctor is Constructor<D> existing)
                return existing;
            else if(delegateType.IsGenericInstanceOf(typeof(Function<,>)) && delegateType.GetGenericArguments().Take(out var argumentsType, out var returnType) == 2L)
            {
                var (parameters, arglist, input) = Signature.Reflect(argumentsType);
                return returnType.IsAssignableFrom(ctor.DeclaringType) && ctor.SignatureEquals(parameters) ? new Constructor<D>(ctor, arglist, new[]{ input }) : null;
            }
            else 
            {
                var invokeMethod = Delegates.GetInvokeMethod<D>();
                return ctor.SignatureEquals(invokeMethod) && invokeMethod.ReturnType.IsAssignableFrom(ctor.DeclaringType) ?
                    new Constructor<D>(ctor, ctor.GetParameterTypes().Map(Expression.Parameter)) :
                    null;
            }
        }
    }
}