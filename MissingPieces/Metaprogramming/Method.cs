using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;

namespace MissingPieces.Metaprogramming
{
    /// <summary>
    /// Represents reflected method.
    /// </summary>
    /// <typeparam name="D">Type of delegate describing signature of the reflected method.</typeparam>
    public sealed class Method<D> : MethodInfo, IMethod<D>, IEquatable<Method<D>>, IEquatable<MethodInfo>
        where D : Delegate
    {
        private readonly MethodInfo method;
        private readonly D invoker;

        internal Method(MethodInfo method, Func<MethodInfo, D> invokerFactory)
        {
            this.method = method;
            invoker = invokerFactory(method);
        }

        public override MethodAttributes Attributes => method.Attributes;
        public override CallingConventions CallingConvention => method.CallingConvention;
        public override bool ContainsGenericParameters => method.ContainsGenericParameters;
        public override Delegate CreateDelegate(Type delegateType) => method.CreateDelegate(delegateType);
        public override Delegate CreateDelegate(Type delegateType, object target) => method.CreateDelegate(delegateType, target);
        public override IEnumerable<CustomAttributeData> CustomAttributes => method.CustomAttributes;
        public override Type DeclaringType => method.DeclaringType;
        public override MethodInfo GetBaseDefinition() => method.GetBaseDefinition();
        public override object[] GetCustomAttributes(bool inherit) => method.GetCustomAttributes(inherit);
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => method.GetCustomAttributes(attributeType, inherit);
        public override IList<CustomAttributeData> GetCustomAttributesData() => method.GetCustomAttributesData();
        public override Type[] GetGenericArguments() => method.GetGenericArguments();
        public override MethodInfo GetGenericMethodDefinition() => method.GetGenericMethodDefinition();
        public override MethodBody GetMethodBody() => method.GetMethodBody();
        public override MethodImplAttributes GetMethodImplementationFlags() => method.GetMethodImplementationFlags();
        public override ParameterInfo[] GetParameters() => method.GetParameters();
        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            => method.Invoke(obj, invokeAttr, binder, parameters, culture);
        public override bool IsDefined(Type attributeType, bool inherit)
            => method.IsDefined(attributeType, inherit);
        public override bool IsGenericMethod => method.IsGenericMethod;
        public override bool IsGenericMethodDefinition => method.IsGenericMethodDefinition;
        public override bool IsSecurityCritical => method.IsSecurityCritical;
        public override bool IsSecuritySafeCritical => method.IsSecuritySafeCritical;
        public override bool IsSecurityTransparent => method.IsSecurityTransparent;
        public override MethodInfo MakeGenericMethod(params Type[] typeArguments) => method.MakeGenericMethod(typeArguments);
        public override MemberTypes MemberType => MemberTypes.Method;
        public override int MetadataToken => method.MetadataToken;
        public override RuntimeMethodHandle MethodHandle => method.MethodHandle;
        public override MethodImplAttributes MethodImplementationFlags => method.MethodImplementationFlags;
        public override Module Module => method.Module;
        public override string Name => method.Name;
        public override Type ReflectedType => method.ReflectedType;
        public override ParameterInfo ReturnParameter => method.ReturnParameter;
        public override Type ReturnType => method.ReturnType;
        public override ICustomAttributeProvider ReturnTypeCustomAttributes => method.ReturnTypeCustomAttributes;

        public bool Equals(MethodInfo other) => method == other;
        public bool Equals(Method<D> other) => Equals(other?.method);

        public override bool Equals(object other)
        {
            switch (other)
            {
                case Method<D> method:
                    return Equals(method);
                case MethodInfo method:
                    return Equals(method);
                default:
                    return false;
            }
        }

        public override int GetHashCode() => method.GetHashCode();

        public static implicit operator D(Method<D> method) => method?.invoker;

        MethodInfo IMember<MethodInfo>.RuntimeMember => method;
        D IMethod<MethodInfo, D>.Invoker => invoker;

        public override string ToString() => method.ToString();
    }
}