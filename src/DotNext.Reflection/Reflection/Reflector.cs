using System;
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
            where D : MulticastDelegate
        {
            private static readonly Func<ConstructorInfo, Constructor<D>> Factory = Constructor<D>.Unreflect;
            private static readonly UserDataSlot<Constructor<D>> UnreflectedConstructor = UserDataSlot<Constructor<D>>.Allocate();

            internal static Constructor<D> GetOrCreate(ConstructorInfo ctor)
                => ctor.GetUserData().GetOrSet(UnreflectedConstructor, ctor, Factory);
        }

        private static class MethodCache<D>
            where D : MulticastDelegate
        {
            private static readonly Func<MethodInfo, Method<D>> Factory = Method<D>.Unreflect;
            private static readonly UserDataSlot<Method<D>> UnreflectedMethod = UserDataSlot<Method<D>>.Allocate();

            internal static Method<D> GetOrCreate(MethodInfo ctor)
                => ctor.GetUserData().GetOrSet(UnreflectedMethod, ctor, Factory);
        }

        /// <summary>
        /// Extracts member metadata from expression tree.
        /// </summary>
        /// <param name="exprTree">Expression tree.</param>
        /// <typeparam name="M">Type of member to reflect.</typeparam>
        /// <returns>Reflected member; or null, if lambda expression doesn't reference a member.</returns>
        public static M MemberOf<M>(Expression<Action> exprTree)
            where M : MemberInfo
        {
            switch(exprTree.Body)
            {
                case MemberExpression body:
                    return body.Member as M;
                case MethodCallExpression body:
                    return body.Method as M;
                case NewExpression body:
                    return body.Constructor as M;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Unreflects constructor to its typed and callable representation.
        /// </summary>
        /// <typeparam name="D">A delegate representing signature of constructor.</typeparam>
        /// <param name="ctor">Constructor to unreflect.</param>
        /// <returns>Unreflected constructor.</returns>
        public static Constructor<D> Unreflect<D>(this ConstructorInfo ctor) where D : MulticastDelegate => ConstructorCache<D>.GetOrCreate(ctor);

        /// <summary>
        /// Unreflects method to its typed and callable representation.
        /// </summary>
        /// <typeparam name="D">A delegate representing signature of method.</typeparam>
        /// <param name="method">A method to unreflect.</param>
        /// <returns>Unreflected method.</returns>
        public static Method<D> Unreflect<D>(this MethodInfo method) where D : MulticastDelegate => MethodCache<D>.GetOrCreate(method);
    }
}