using System;
using System.Globalization;
using System.Collections.Generic;
using static System.Linq.Expressions.Expression;
using System.Reflection;

namespace MissingPieces.Metaprogramming
{
    using VariantType;
    using static Reflection.Methods;

    /// <summary>
    /// Provides constructor definition based on delegate signature.
    /// </summary>
    /// <typeparam name="D">Type of delegate representing constructor of type <typeparamref name="D"/>.</typeparam>
    public sealed class Constructor<D> : ConstructorInfo, IConstructor<D>, IEquatable<ConstructorInfo>, IEquatable<Constructor<D>>
        where D : class, MulticastDelegate
    {
        private readonly D invoker;
        private readonly Variant<ConstructorInfo, Type> ctorOrDeclaringType;

        private Constructor(ConstructorInfo ctor)
        {
            ctorOrDeclaringType = ctor;
            var parameters = ctor.GetParameters().Map(p => Parameter(p.ParameterType));
            invoker = Lambda<D>(New(ctor), parameters).Compile();
        }

        private Constructor(Type valueType)
        {
            ctorOrDeclaringType = valueType;
            invoker = Lambda<D>(Default(valueType)).Compile();
        }

        public static implicit operator D(Constructor<D> ctor) => ctor?.invoker;

        public override string Name => ctorOrDeclaringType.First.GetOrDefault()?.Name ?? ".ctor";

        ConstructorInfo IMember<ConstructorInfo>.RuntimeMember => (ConstructorInfo)ctorOrDeclaringType;

        D IMethod<ConstructorInfo, D>.Invoker => invoker;

        public override MethodAttributes Attributes => ctorOrDeclaringType.First.Map(ctor => ctor.Attributes).GetOrDefault(invoker.Method.Attributes);

        public override RuntimeMethodHandle MethodHandle => ctorOrDeclaringType.First.Map(ctor => ctor.MethodHandle).GetOrDefault(invoker.Method.MethodHandle);

        public override Type DeclaringType => ctorOrDeclaringType.UnifyFirst(ctor => ctor.DeclaringType);

        public override Type ReflectedType => ctorOrDeclaringType.First.Map(ctor => ctor.ReflectedType).GetOrDefault(invoker.Method.ReflectedType);

        public override CallingConventions CallingConvention => ctorOrDeclaringType.First.Map(ctor => ctor.CallingConvention).GetOrDefault(invoker.Method.CallingConvention);


        public override bool ContainsGenericParameters => false;

        public override IEnumerable<CustomAttributeData> CustomAttributes => GetCustomAttributesData();

        public override MethodBody GetMethodBody() => ctorOrDeclaringType.First.Map(ctor => ctor.GetMethodBody()).GetOrInvoke(invoker.Method.GetMethodBody);

        public override IList<CustomAttributeData> GetCustomAttributesData() => ctorOrDeclaringType.First.Map(ctor => ctor.GetCustomAttributesData()).GetOrInvoke(Array.Empty<CustomAttributeData>);

        public override Type[] GetGenericArguments() => Array.Empty<Type>();

        public override bool IsGenericMethod => false;

        public override bool IsGenericMethodDefinition => false;

        public override bool IsSecurityCritical => ctorOrDeclaringType.First.Map(ctor => ctor.IsSecurityCritical).GetOrDefault(invoker.Method.IsSecurityCritical);

        public override bool IsSecuritySafeCritical => ctorOrDeclaringType.First.Map(ctor => ctor.IsSecuritySafeCritical).GetOrDefault(invoker.Method.IsSecuritySafeCritical);

        public override bool IsSecurityTransparent => ctorOrDeclaringType.First.Map(ctor => ctor.IsSecurityTransparent).GetOrDefault(invoker.Method.IsSecurityTransparent);

        public override MemberTypes MemberType => MemberTypes.Constructor;

        public override int MetadataToken => ctorOrDeclaringType.First.Map(ctor => ctor.MetadataToken).GetOrDefault(invoker.Method.MetadataToken);

        public override MethodImplAttributes MethodImplementationFlags => ctorOrDeclaringType.First.Map(ctor => ctor.MethodImplementationFlags).GetOrDefault(invoker.Method.MethodImplementationFlags);

        public override Module Module => ctorOrDeclaringType.UnifyFirst(ctor => ctor.DeclaringType).Module;

        public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            => Invoke(null, invokeAttr, binder, parameters, culture);

        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;

        public override ParameterInfo[] GetParameters() => ctorOrDeclaringType.First.Map(ctor => ctor.GetParameters()).GetOrInvoke(Array.Empty<ParameterInfo>);

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            var ctor = (ConstructorInfo)ctorOrDeclaringType;
            return ctor == null ? invoker.Method.Invoke(obj, invokeAttr, binder, parameters, culture) : ctor.Invoke(obj, invokeAttr, binder, parameters, culture);
        }

        public override object[] GetCustomAttributes(bool inherit)
            => ctorOrDeclaringType.First.Map(ctor => ctor.GetCustomAttributes(inherit)).GetOrInvoke(Array.Empty<object>);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            => ctorOrDeclaringType.First.Map(ctor => ctor.GetCustomAttributes(attributeType, inherit)).GetOrInvoke(Array.Empty<object>);

        public override bool IsDefined(Type attributeType, bool inherit)
            => ctorOrDeclaringType.First.Map(ctor => ctor.IsDefined(attributeType, inherit)).GetOrDefault(false);

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

        public override string ToString() => ctorOrDeclaringType.First.Map(ctor => ctor.ToString()).GetOrInvoke(invoker.Method.ToString);

        public override int GetHashCode() => ctorOrDeclaringType.GetHashCode();

        internal static Constructor<D> Create(Type declaringType, bool nonPublic)
        {
            var invokeMethod = Delegates.GetInvokeMethod<D>();

            if (declaringType.IsValueType && invokeMethod.GetParameters().LongLength == 0L)
                return new Constructor<D>(declaringType);
            else
            {
                var flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | (nonPublic ? BindingFlags.NonPublic : BindingFlags.Public);
                var ctor = declaringType.GetConstructor(flags, Type.DefaultBinder, invokeMethod.GetParameterTypes(), Array.Empty<ParameterModifier>());
                return ctor is null || !invokeMethod.ReturnType.IsAssignableFrom(declaringType) ?
                    null :
                    new Constructor<D>(ctor);
            }
        }
    }
}