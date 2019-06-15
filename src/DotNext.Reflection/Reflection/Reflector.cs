using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides access to fast reflection routines.
    /// </summary>
    public static class Reflector
    {
        /// <summary>
        /// Extracts member metadata from expression tree.
        /// </summary>
        /// <param name="exprTree">Expression tree.</param>
        /// <typeparam name="M">Type of member to reflect.</typeparam>
        /// <returns>Reflected member; or null, if lambda expression doesn't reference a member.</returns>
        public static M MemberOf<M>(Expression<Action> exprTree)
            where M : MemberInfo
        {
            switch (exprTree.Body)
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
        public static Constructor<D> Unreflect<D>(this ConstructorInfo ctor) where D : MulticastDelegate => Constructor<D>.GetOrCreate(ctor);

        /// <summary>
        /// Unreflects method to its typed and callable representation.
        /// </summary>
        /// <typeparam name="D">A delegate representing signature of method.</typeparam>
        /// <param name="method">A method to unreflect.</param>
        /// <returns>Unreflected method.</returns>
        public static Method<D> Unreflect<D>(this MethodInfo method) where D : MulticastDelegate => Method<D>.GetOrCreate(method);
    }
}