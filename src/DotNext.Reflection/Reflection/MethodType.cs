using System;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents method declaration type.
    /// </summary>
    public abstract class MethodType
    {
        private protected abstract class Cache<T, D> : MemberCache<MethodInfo, Method<D>>
            where D : MulticastDelegate
        {
            private protected bool nonPublic;

            private protected Cache(bool nonPublic) => this.nonPublic = nonPublic;
        }

        private protected MethodType() { }

        private protected abstract Cache<T, D> GetPublicMethods<T, D>() where D : MulticastDelegate;

        private protected abstract Cache<T, D> GetNonPublicMethods<T, D>() where D : MulticastDelegate;

        internal Method<D> GetOrCreate<T, D>(string methodName, bool nonPublic) 
            where D : MulticastDelegate
            => (nonPublic ? GetNonPublicMethods<T, D>() : GetPublicMethods<T, D>()).GetOrCreate(methodName);
        
        /// <summary>
		/// Represents static method.
		/// </summary>
        public static readonly MethodType Static = new StaticMethodType();

        /// <summary>
		/// Represents instance method.
		/// </summary>
        public static readonly MethodType Instance = new InstanceMethodType();
    }

    internal sealed class StaticMethodType : MethodType
    {
        private new class Cache<T, D> : MethodType.Cache<T, D>
            where D : MulticastDelegate
        {
            internal Cache(bool nonPublic) : base(nonPublic) { }

            private protected sealed override Reflection.Method<D> Create(string methodName)
                => Method<D>.Reflect<T>(methodName, nonPublic);
        }

        private sealed class PublicMethodsCache<T, D> : Cache<T, D>
            where D : MulticastDelegate
        {
            internal static readonly PublicMethodsCache<T, D> Instance = new PublicMethodsCache<T, D>();

            private PublicMethodsCache() : base(false) { }
        }

        private sealed class NonPublicMethodsCache<T, D> : Cache<T, D>
            where D : MulticastDelegate
        {
            internal static readonly NonPublicMethodsCache<T, D> Instance = new NonPublicMethodsCache<T, D>();

            private NonPublicMethodsCache() : base(true) { }
        }

        private protected override MethodType.Cache<T, D> GetPublicMethods<T, D>() => PublicMethodsCache<T, D>.Instance;

        private protected override MethodType.Cache<T, D> GetNonPublicMethods<T, D>() => NonPublicMethodsCache<T, D>.Instance;
    }

    internal sealed class InstanceMethodType : MethodType
    {
        private new class Cache<T, D> : MethodType.Cache<T, D>
            where D : MulticastDelegate
        {
            internal Cache(bool nonPublic) : base(nonPublic) { }

            private protected sealed override Reflection.Method<D> Create(string methodName)
                => Method<D>.Reflect(methodName, nonPublic);
        }

        private sealed class PublicMethodsCache<T, D> : Cache<T, D>
            where D : MulticastDelegate
        {
            internal static readonly PublicMethodsCache<T, D> Instance = new PublicMethodsCache<T, D>();

            private PublicMethodsCache() : base(false) { }
        }

        private sealed class NonPublicMethodsCache<T, D> : Cache<T, D>
            where D : MulticastDelegate
        {
            internal static readonly NonPublicMethodsCache<T, D> Instance = new NonPublicMethodsCache<T, D>();

            private NonPublicMethodsCache() : base(true) { }
        }

        private protected override MethodType.Cache<T, D> GetPublicMethods<T, D>() => PublicMethodsCache<T, D>.Instance;

        private protected override MethodType.Cache<T, D> GetNonPublicMethods<T, D>() => NonPublicMethodsCache<T, D>.Instance;
    }
}