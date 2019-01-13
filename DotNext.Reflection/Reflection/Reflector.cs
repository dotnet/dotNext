using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides access to fast reflection routines.
    /// </summary>
    public static class Reflector
    {
        private static class ConstructorCache<D>
            where D: MulticastDelegate
        {
            private static readonly ConditionalWeakTable<ConstructorInfo, Constructor<D>> constructors =
                new ConditionalWeakTable<ConstructorInfo, Constructor<D>>();
            
            internal static Constructor<D> GetOrCreate(ConstructorInfo ctor)
                => constructors.GetValue(ctor, Constructor<D>.Unreflect);
            
        }

        private static class MethodCache<D>
            where D: MulticastDelegate
        {
            private static readonly ConditionalWeakTable<MethodInfo, Method<D>> constructors =
                new ConditionalWeakTable<MethodInfo, Method<D>>();
            
            internal static Method<D> GetOrCreate(MethodInfo ctor)
                => constructors.GetValue(ctor, Method<D>.Unreflect);
        }

        /// <summary>
        /// Extracts member metadata from expression tree.
        /// </summary>
        /// <param name="exprTree">Expression tree.</param>
        /// <typeparam name="M">Type of member to reflect.</typeparam>
        /// <returns>Reflected member; or null, if lambda expression doesn't reference a member.</returns>
        public static M MemberOf<M>(Expression<Action> exprTree)
            where M: MemberInfo
        {
            if(exprTree.Body is MemberExpression member)
                return member.Member as M;
            else if(exprTree.Body is MethodCallExpression method)
                return method.Method as M;
            else if(exprTree.Body is NewExpression ctor)
                return ctor.Constructor as M;
            else
                return null;
        }

		/// <summary>
		/// Unreflects constructor to its typed and callable representation.
		/// </summary>
		/// <typeparam name="D">A delegate representing signature of constructor.</typeparam>
		/// <param name="ctor">Constructor to unreflect.</param>
		/// <returns>Unreflected constructor.</returns>
		public static Constructor<D> Unreflect<D>(this ConstructorInfo ctor)
            where D: MulticastDelegate
            => ConstructorCache<D>.GetOrCreate(ctor);

		/// <summary>
		/// Unreflects method to its typed and callable representation.
		/// </summary>
		/// <typeparam name="D">A delegate representing signature of method.</typeparam>
		/// <param name="method">A method to unreflect.</param>
		/// <returns>Unreflected method.</returns>
		public static Method<D> Unreflect<D>(this MethodInfo method)
            where D: MulticastDelegate
            => MethodCache<D>.GetOrCreate(method);
    }
}