using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace MissingPieces.Reflection
{
    /// <summary>
    /// Reflection methods based on LINQ expression tree.
    /// </summary>
    public static class Reflector
    {
        private sealed class RuntimeMethodHandleEqualityComparer: IEqualityComparer<RuntimeMethodHandle>
        {
            public int GetHashCode(RuntimeMethodHandle handle) => handle.GetHashCode();
            public bool Equals(RuntimeMethodHandle first, RuntimeMethodHandle second) => first.Equals(second);
        }

        private sealed class ConstructorCache<D>: Cache<RuntimeMethodHandle, ConstructorInfo, Constructor<D>>
            where D: MulticastDelegate
        {
            private static readonly Cache<RuntimeMethodHandle, ConstructorInfo, Constructor<D>> Instance = new ConstructorCache<D>();

            private ConstructorCache()
                : base(ctor => ctor.MethodHandle, new RuntimeMethodHandleEqualityComparer())
            {
            }

            private protected override Constructor<D> Create(ConstructorInfo ctor) => Constructor<D>.Reflect(ctor);

            internal new static Constructor<D> GetOrCreate(ConstructorInfo ctor) => Instance.GetOrCreate(ctor);
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
        /// Binds constructor to its typed representation.
        /// </summary>
        /// <param name="ctor">Constructor to reflect.</param>
        /// <typeparam name="D">A delegate representing signature of constructor.</typeparam>
        /// <returns></returns>
        public static Constructor<D> Bind<D>(this ConstructorInfo ctor)
            where D: MulticastDelegate
            => ConstructorCache<D>.GetOrCreate(ctor);
    }
}