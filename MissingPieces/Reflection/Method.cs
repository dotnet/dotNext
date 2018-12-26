using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using static System.Linq.Enumerable;
using static System.Linq.Expressions.Expression;

namespace MissingPieces.Reflection
{
    using Reflection;

    /// <summary>
    /// Represents reflected method.
    /// </summary>
    /// <typeparam name="D">Type of delegate describing signature of the reflected method.</typeparam>
    public sealed class Method<D> : MethodInfo, IMethod<D>, IEquatable<Method<D>>, IEquatable<MethodInfo>
        where D : Delegate
    {
        private readonly MethodInfo method;
        private readonly D invoker;

        private Method(MethodInfo method, D invoker)
        {
            this.method = method;
            this.invoker = invoker;
        }

        internal Method<D> OfType<T>() => method.DeclaringType.IsAssignableFrom(typeof(T)) ? this : null;

        public sealed override MethodAttributes Attributes => method.Attributes;
        public sealed override CallingConventions CallingConvention => method.CallingConvention;
        public sealed override bool ContainsGenericParameters => method.ContainsGenericParameters;
        public sealed override Delegate CreateDelegate(Type delegateType) => method.CreateDelegate(delegateType);
        public sealed override Delegate CreateDelegate(Type delegateType, object target) => method.CreateDelegate(delegateType, target);
        public sealed override IEnumerable<CustomAttributeData> CustomAttributes => method.CustomAttributes;
        public sealed override Type DeclaringType => method.DeclaringType;
        public sealed override MethodInfo GetBaseDefinition() => method.GetBaseDefinition();
        public sealed override object[] GetCustomAttributes(bool inherit) => method.GetCustomAttributes(inherit);
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => method.GetCustomAttributes(attributeType, inherit);
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => method.GetCustomAttributesData();
        public sealed override Type[] GetGenericArguments() => method.GetGenericArguments();
        public sealed override MethodInfo GetGenericMethodDefinition() => method.GetGenericMethodDefinition();
        public sealed override MethodBody GetMethodBody() => method.GetMethodBody();
        public sealed override MethodImplAttributes GetMethodImplementationFlags() => method.GetMethodImplementationFlags();
        public sealed override ParameterInfo[] GetParameters() => method.GetParameters();
        public sealed override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            => method.Invoke(obj, invokeAttr, binder, parameters, culture);
        public sealed override bool IsDefined(Type attributeType, bool inherit)
            => method.IsDefined(attributeType, inherit);
        public sealed override bool IsGenericMethod => method.IsGenericMethod;
        public sealed override bool IsGenericMethodDefinition => method.IsGenericMethodDefinition;
        public sealed override bool IsSecurityCritical => method.IsSecurityCritical;
        public sealed override bool IsSecuritySafeCritical => method.IsSecuritySafeCritical;
        public sealed override bool IsSecurityTransparent => method.IsSecurityTransparent;
        public sealed override MethodInfo MakeGenericMethod(params Type[] typeArguments) => method.MakeGenericMethod(typeArguments);
        public sealed override MemberTypes MemberType => MemberTypes.Method;
        public sealed override int MetadataToken => method.MetadataToken;
        public sealed override RuntimeMethodHandle MethodHandle => method.MethodHandle;
        public sealed override MethodImplAttributes MethodImplementationFlags => method.MethodImplementationFlags;
        public sealed override Module Module => method.Module;
        public sealed override string Name => method.Name;
        public sealed override Type ReflectedType => method.ReflectedType;
        public sealed override ParameterInfo ReturnParameter => method.ReturnParameter;
        public sealed override Type ReturnType => method.ReturnType;
        public sealed override ICustomAttributeProvider ReturnTypeCustomAttributes => method.ReturnTypeCustomAttributes;

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
        D ICallable<D>.Invoker => invoker;

        public override string ToString() => method.ToString();

        private static Method<D> Reflect(Type declaringType, string methodName, bool nonPublic)
        {
            const BindingFlags PublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
            const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            var (parameters, returnType) = Delegates.GetInvokeMethod<D>().Decompose(method => method.GetParameterTypes(), method => method.ReturnType);
            var targetMethod = declaringType.GetMethod(methodName,
                nonPublic ? NonPublicFlags : PublicFlags,
                Type.DefaultBinder,
                parameters,
                Array.Empty<ParameterModifier>());
            if (targetMethod is null)
                return null;
            else if (returnType == targetMethod.ReturnType)
                return new Method<D>(targetMethod, targetMethod.CreateDelegate<D>());
            else
                return null;
        }

        /// <summary>
        /// Reflects instance method.
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="nonPublic"></param>
        /// <returns></returns>
        internal static Method<D> Reflect(string methodName, bool nonPublic)
        {
            if(typeof(D).IsAbstract)
                return null;

            const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
            const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            var (parameters, returnType) = Delegates.GetInvokeMethod<D>().Decompose(method => method.GetParameterTypes(), method => method.ReturnType);
            var thisParam = parameters.FirstOrDefault() ?? throw new ArgumentException("Delegate type should have THIS parameter");
            var targetMethod = thisParam.NonRefType().GetMethod(methodName,
                nonPublic ? NonPublicFlags : PublicFlags,
                Type.DefaultBinder,
                parameters.RemoveFirst(1),  //remove hidden this parameter
                Array.Empty<ParameterModifier>());
            if (targetMethod is null)
                return null;
            //this parameter can be passed as REF so handle this situation
            //first parameter should be passed by REF for structure types
            Func<MethodInfo, D> invokerFactory;
            if (thisParam.IsByRef)
            {
                thisParam = thisParam.GetElementType();
                var formalParams = parameters.Map(Parameter);
                invokerFactory = thisParam.IsValueType ?
                    new Func<MethodInfo, D>(targetMethod.CreateDelegate<D>) :
                    method => Lambda<D>(Call(formalParams[0], method, formalParams.RemoveFirst(1)), formalParams).Compile();
            }
            else if (thisParam.IsValueType)
            {
                var formalParams = parameters.Map(Parameter);
                invokerFactory = method => Lambda<D>(Call(formalParams[0], targetMethod, formalParams.RemoveFirst(1)), formalParams).Compile();
            }
            else
                invokerFactory = targetMethod.CreateDelegate<D>;
            return returnType == targetMethod.ReturnType ?
                    new Method<D>(targetMethod, invokerFactory(targetMethod)) :
                    null;
        }

        /// <summary>
        /// Reflects static method.
        /// </summary>
        /// <param name="methodName">Name of method.</param>
        /// <param name="nonPublic">True to reflect non-public static method.</param>
        /// <typeparam name="T">Declaring type.</typeparam>
        /// <returns>Reflected static method.</returns>
        internal static Method<D> Reflect<T>(string methodName, bool nonPublic)
            => typeof(D).IsAbstract ? null : Reflect(typeof(T), methodName, nonPublic);
    }
}