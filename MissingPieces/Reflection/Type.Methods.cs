using System;
using System.Reflection;

namespace MissingPieces.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides access to methods declared in type <typeparamref name="T"/>.
        /// </summary>
        public static class Method
        {
            private sealed class InstanceMethods<D> : MemberCache<MethodInfo, Reflection.Method<D>>
                where D: Delegate
            {
                internal static readonly InstanceMethods<D> Public = new InstanceMethods<D>(false);
                internal static readonly InstanceMethods<D> NonPublic = new InstanceMethods<D>(true);

                private readonly bool nonPublic;
                private InstanceMethods(bool nonPublic) => this.nonPublic = nonPublic;

                private protected override Reflection.Method<D> Create(string methodName) 
                    => Reflection.Method<D>.Reflect(methodName, nonPublic)?.OfType<T>();
            }

            private sealed class StaticMethods<D> : MemberCache<MethodInfo, Reflection.Method<D>>
                where D: Delegate
            {
                internal static readonly StaticMethods<D> Public = new StaticMethods<D>(false);
                internal static readonly StaticMethods<D> NonPublic = new StaticMethods<D>(true);
                private readonly bool nonPublic;
                private StaticMethods(bool nonPublic) => this.nonPublic = nonPublic;

                private protected override Reflection.Method<D> Create(string eventName) 
                    => Reflection.Method<D>.Reflect<T>(eventName, nonPublic);
            }

            public static Reflection.Method<D> Get<D>(string methodName, bool nonPublic = false)
                where D: Delegate
                => (nonPublic ? InstanceMethods<D>.NonPublic : InstanceMethods<D>.Public).GetOrCreate(methodName);

            public static Reflection.Method<D> GetStatic<D>(string methodName, bool nonPublic = false)
                where D: Delegate
                => (nonPublic ? StaticMethods<D>.NonPublic : StaticMethods<D>.Public).GetOrCreate(methodName);
        }
        
        /// <summary>
        /// Provides access to methods with single parameter declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P">Type of method parameter.</typeparam>
        public static class Method<P>
        {
            public static Reflection.Method<Action<T, P>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P>>(methodName, nonPublic);

            public static Reflection.Method<Action<T, P>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.CreateAction<T, P>(methodName);

            public static Reflection.Method<Action<P>> GetStatic(string methodName, bool nonPublic = false)
                => Method.GetStatic<Action<P>>(methodName, nonPublic);

            public static Reflection.Method<Action<P>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.CreateAction<T, P>(methodName);
            
            public static Reflection.Method<Func<T, P, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P, R>>(methodName, nonPublic);

            public static Reflection.Method<Func<T, P, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.CreateAction<T, P>(methodName);

            public static Reflection.Method<Func<P, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.GetStatic<Func<P, R>>(methodName, nonPublic);

            public static Reflection.Method<Func<P, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.CreateAction<T, P>(methodName);
        }
    }
}