using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents reflected method.
    /// </summary>
    /// <typeparam name="D">Type of delegate describing signature of the reflected method.</typeparam>
    public sealed class Method<D> : MethodInfo, IMethod<D>, IEquatable<Method<D>>, IEquatable<MethodInfo>
        where D : MulticastDelegate
    {
        private const BindingFlags StaticPublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags StaticNonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags InstancePublicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags InstanceNonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly MethodInfo method;
        private readonly D invoker;

        private Method(MethodInfo method, Expression[] args, ParameterExpression[] parameters)
        {
            this.method = method;
            invoker = Expression.Lambda<D>(Expression.Call(method, args), parameters).Compile();
        }

        private Method(MethodInfo method, ParameterExpression instance, Expression[] args, ParameterExpression[] parameters)
        {
            this.method = method;
            invoker = Expression.Lambda<D>(Expression.Call(instance, method, args), parameters.Insert(instance, 0)).Compile();
        }

        private Method(MethodInfo method)
        {
            this.method = method;
            this.invoker = Delegates.CreateDelegate<D>(method);
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
        D IMember<MethodInfo, D>.Invoker => invoker;

        public override string ToString() => method.ToString();

        private static Method<D> ReflectStatic(Type declaringType, Type[] parameters, Type returnType, string methodName, bool nonPublic)
        {
            var targetMethod = declaringType.GetMethod(methodName,
                nonPublic ? StaticNonPublicFlags : StaticPublicFlags,
                Type.DefaultBinder,
                parameters,
                Array.Empty<ParameterModifier>());
            return targetMethod is null || returnType != targetMethod.ReturnType ? null : new Method<D>(targetMethod);
        }

        private static Method<D> ReflectStatic(Type declaringType, Type argumentsType, Type returnType, string methodName, bool nonPublic)
        {
            var (parameters, arglist, input) = Signature.Reflect(argumentsType);
            var targetMethod = declaringType.GetMethod(methodName,
                nonPublic ? StaticNonPublicFlags : StaticPublicFlags,
                Type.DefaultBinder,
                parameters,
                Array.Empty<ParameterModifier>());
            return targetMethod is null || returnType != targetMethod.ReturnType ? null : new Method<D>(targetMethod, arglist, new[]{ input });
        }

		private static Type NonRefType(Type type) => type.IsByRef ? type.GetElementType() : type;

		private static Method<D> ReflectInstance(Type thisParam, Type[] parameters, Type returnType, string methodName, bool nonPublic)
        {
            var targetMethod = NonRefType(thisParam).GetMethod(methodName,
                nonPublic ? InstanceNonPublicFlags : InstancePublicFlags,
                Type.DefaultBinder,
                parameters, 
                Array.Empty<ParameterModifier>());

            //this parameter can be passed as REF so handle this situation
            //first parameter should be passed by REF for structure types
            if(targetMethod is null || returnType != targetMethod.ReturnType)
                return null;
            else if(thisParam.IsByRef ^ thisParam.IsValueType)
            {
                var thisParamDeclaration = Expression.Parameter(thisParam);
                var parametersDeclaration = parameters.Convert(Expression.Parameter);
                return new Method<D>(targetMethod, thisParamDeclaration, parametersDeclaration, parametersDeclaration);
            }
            else
                return new Method<D>(targetMethod);
        }

        private static Method<D> ReflectInstance(Type thisParam, Type argumentsType, Type returnType, string methodName, bool nonPublic)
        {
            var (parameters, arglist, input) = Signature.Reflect(argumentsType);
            var thisParamDeclaration = Expression.Parameter(thisParam.MakeByRefType());
            var targetMethod = thisParam.GetMethod(methodName,
                nonPublic ? InstanceNonPublicFlags : InstancePublicFlags,
                Type.DefaultBinder,
                parameters, 
                Array.Empty<ParameterModifier>());
            return targetMethod is null || returnType != targetMethod.ReturnType ? null : new Method<D>(targetMethod, thisParamDeclaration, arglist, new[]{ input });
        }

        /// <summary>
        /// Reflects instance method.
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="nonPublic"></param>
        /// <returns></returns>
        internal static Method<D> Reflect(string methodName, bool nonPublic)
        {
            var delegateType = typeof(D);
			if (delegateType.IsAbstract)
				throw new AbstractDelegateException<D>();
			else if (delegateType.IsGenericInstanceOf(typeof(Function<,,>)) && delegateType.GetGenericArguments().Take(out var thisParam, out var argumentsType, out var returnType) == 3L)
				return ReflectInstance(thisParam, argumentsType, returnType, methodName, nonPublic);
            else if(delegateType.IsGenericInstanceOf(typeof(Procedure<,>)) && delegateType.GetGenericArguments().Take(out thisParam, out argumentsType) == 2L)
                return ReflectInstance(thisParam, argumentsType, typeof(void), methodName, nonPublic);
			else
			{
				Delegates.GetInvokeMethod<D>().Decompose(Methods.GetParameterTypes, method => method.ReturnType, out var parameters, out returnType);
				thisParam = parameters.FirstOrDefault() ?? throw new ArgumentException("Delegate type should have THIS parameter");
				return ReflectInstance(thisParam, parameters.RemoveFirst(1), returnType, methodName, nonPublic);
			}
        }

        /// <summary>
        /// Reflects static method.
        /// </summary>
        /// <param name="methodName">Name of method.</param>
        /// <param name="nonPublic">True to reflect non-public static method.</param>
        /// <typeparam name="T">Declaring type.</typeparam>
        /// <returns>Reflected static method.</returns>
        internal static Method<D> Reflect<T>(string methodName, bool nonPublic)
        {
            var delegateType = typeof(D);
			if (delegateType.IsAbstract)
				throw new AbstractDelegateException<D>();
			else if (delegateType.IsGenericInstanceOf(typeof(Function<,>)) && delegateType.GetGenericArguments().Take(out var argumentsType, out var returnType) == 2L)
				return ReflectStatic(typeof(T), argumentsType, returnType, methodName, nonPublic);
            else if(delegateType.IsGenericInstanceOf(typeof(Procedure<>)))
                return ReflectStatic(typeof(T), delegateType.GetGenericArguments()[0], typeof(void), methodName, nonPublic);
			else
			{
				Delegates.GetInvokeMethod<D>().Decompose(Methods.GetParameterTypes, method => method.ReturnType, out var parameters, out returnType);
				return ReflectStatic(typeof(T), parameters, returnType, methodName, nonPublic);
			}
        }

        private static Method<D> UnreflectStatic(MethodInfo method)
        {
            var delegateType = typeof(D);
            if(delegateType.IsGenericInstanceOf(typeof(Function<,>)) && delegateType.GetGenericArguments().Take(out var argumentsType, out var returnType) == 2L)
            {
                var (parameters, arglist, input) = Signature.Reflect(argumentsType);
                return returnType == method.ReturnType && method.SignatureEquals(parameters) ? new Method<D>(method, arglist, new[]{ input }) : null;
            }
            else if(delegateType.IsGenericInstanceOf(typeof(Procedure<>)))
            {
                argumentsType = delegateType.GetGenericArguments()[0];
                var (parameters, arglist, input) = Signature.Reflect(argumentsType);
                return typeof(void) == method.ReturnType && method.SignatureEquals(parameters) ? new Method<D>(method, arglist, new[]{ input }) : null; 
            }
			else if(Delegates.GetInvokeMethod<D>().SignatureEquals(method))
                return new Method<D>(method);
            else
                return null;
        }

        private static Method<D> UnreflectInstance(MethodInfo method)
        {
            var delegateType = typeof(D);
            if(delegateType.IsGenericInstanceOf(typeof(Function<,,>)) && delegateType.GetGenericArguments().Take(out var thisParam, out var argumentsType, out var returnType) == 3L)
            {
                var (parameters, arglist, input) = Signature.Reflect(argumentsType);
                return thisParam.IsAssignableFrom(method.DeclaringType) && returnType == method.ReturnType && method.SignatureEquals(parameters) ? new Method<D>(method, Expression.Parameter(thisParam.MakeByRefType(), "this"), arglist, new[]{ input }) : null;
            }
            else if(delegateType.IsGenericInstanceOf(typeof(Procedure<,>)) && delegateType.GetGenericArguments().Take(out thisParam, out argumentsType) == 2L)
            {
                var (parameters, arglist, input) = Signature.Reflect(argumentsType);
                return thisParam.IsAssignableFrom(method.DeclaringType) && typeof(void) == method.ReturnType && method.SignatureEquals(parameters) ? new Method<D>(method, Expression.Parameter(thisParam.MakeByRefType(), "this"), arglist, new[]{ input }) : null;
            }
			else if(delegateType.IsGenericInstanceOf(typeof(MemberGetter<,>)) && delegateType.GetGenericArguments().Take(out thisParam, out returnType) == 2L)
			{
				var thisParamDecl = Expression.Parameter(thisParam.MakeByRefType(), "this");
				return method.DeclaringType.IsAssignableFrom(thisParam) && returnType == method.ReturnType && method.GetParameters().IsNullOrEmpty() ? new Method<D>(method, thisParamDecl, Array.Empty<Expression>(), Array.Empty<ParameterExpression>()) : null;
			}
			else if(delegateType.IsGenericInstanceOf(typeof(MemberSetter<,>)) && delegateType.GetGenericArguments().Take(out thisParam, out argumentsType) == 2L)
			{
				var thisParamDecl = Expression.Parameter(thisParam.MakeByRefType(), "this");
				var argDecl = Expression.Parameter(argumentsType);
				return thisParam.IsAssignableFrom(method.DeclaringType) && method.GetParameterTypes().FirstOrDefault() == argumentsType && method.ReturnType == typeof(void) ? new Method<D>(method, thisParamDecl, new[] { argDecl }, new[] { argDecl }) : null;
			}
            else
            {
                Delegates.GetInvokeMethod<D>().Decompose(Methods.GetParameterTypes, m => m.ReturnType, out var parameters, out returnType);
                thisParam = parameters.FirstOrDefault() ?? throw new ArgumentException("Delegate type should have THIS parameter");
                parameters = parameters.RemoveFirst(1);
                return method.SignatureEquals(parameters) && method.ReturnType == returnType && method.DeclaringType.IsAssignableFrom(thisParam) ?
                    new Method<D>(method) :
                    null;
            }
        }

        internal static Method<D> Unreflect(MethodInfo method)
        {
            var delegateType = typeof(D);
			if (delegateType.IsAbstract)
				throw new AbstractDelegateException<D>();
			else if (method is Method<D> existing)
				return existing;
			else if (method.IsGenericMethodDefinition || method.IsAbstract || method.IsConstructor)
				return null;
			else if (method.IsStatic)
				return UnreflectStatic(method);
			else
				return UnreflectInstance(method);
        }
    }
}