using System;
using System.Reflection;

namespace Cheats.Reflection
{
    public static partial class Type<T>
    {
        public static Reflection.Method<Function<A, R>> GetStaticMethod<A, R>(string methodName, bool nonPublic = false)
            where A: struct
            => Method.Get<Function<A, R>>(methodName, MemberLookup.Static, nonPublic);
        
        public static Reflection.Method<Function<A, R>> RequireStaticMethod<A, R>(string methodName, bool nonPublic = false)
            where A: struct
            => GetStaticMethod<A, R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, A, R>(methodName);

        public static Reflection.Method<Function<T, A, R>> GetMethod<A, R>(string methodName, bool nonPublic = false)
            where A: struct
            => Method.Get<Function<T, A, R>>(methodName, MemberLookup.Instance, nonPublic);
        
        public static Reflection.Method<Function<T, A, R>> RequireMethod<A, R>(string methodName, bool nonPublic = false)
            where A: struct
            => GetMethod<A, R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, A, R>(methodName);

        /// <summary>
        /// Provides access to methods declared in type <typeparamref name="T"/>.
        /// </summary>
        public static class Method
        {
            private sealed class InstanceMethods<D> : MemberCache<MethodInfo, Reflection.Method<D>>
                where D: MulticastDelegate
            {
                internal static readonly InstanceMethods<D> Public = new InstanceMethods<D>(false);
                internal static readonly InstanceMethods<D> NonPublic = new InstanceMethods<D>(true);

                private readonly bool nonPublic;
                private InstanceMethods(bool nonPublic) => this.nonPublic = nonPublic;

                private protected override Reflection.Method<D> Create(string methodName) 
                    => Reflection.Method<D>.Reflect(methodName, nonPublic)?.OfType<T>();
            }

            private sealed class StaticMethods<D> : MemberCache<MethodInfo, Reflection.Method<D>>
                where D: MulticastDelegate
            {
                internal static readonly StaticMethods<D> Public = new StaticMethods<D>(false);
                internal static readonly StaticMethods<D> NonPublic = new StaticMethods<D>(true);
                private readonly bool nonPublic;
                private StaticMethods(bool nonPublic) => this.nonPublic = nonPublic;

                private protected override Reflection.Method<D> Create(string methodName) 
                    => Reflection.Method<D>.Reflect<T>(methodName, nonPublic);
            }

            public static Reflection.Method<D> Get<D>(string methodName, MemberLookup methodType, bool nonPublic = false)
                where D: MulticastDelegate
            {
                MemberCache<MethodInfo, Reflection.Method<D>> cache;
                switch(methodType)
                {
                    case MemberLookup.Static:
                        cache = nonPublic ? StaticMethods<D>.NonPublic : StaticMethods<D>.Public;
                        break;
                    case MemberLookup.Instance:
                        cache = nonPublic ? InstanceMethods<D>.NonPublic : InstanceMethods<D>.Public;
                        break;
                    default:
                        return null;
                }
                return cache.GetOrCreate(methodName);
            }

            public static Reflection.Method<D> Require<D>(string methodName, MemberLookup methodType, bool nonPublic = false)
                where D: MulticastDelegate
                => Get<D>(methodName, methodType, nonPublic) ?? throw MissingMethodException.Create<D>(methodName);
            
            public static Reflection.Method<Action<T>> Get(string methodName, bool nonPublic = false)
                => Get<Action<T>>(methodName, MemberLookup.Instance, nonPublic);

            public static Reflection.Method<Action<T>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action>(methodName);
            
            public static Reflection.Method<Action> GetStatic(string methodName, bool nonPublic = false)
                => Get<Action>(methodName, MemberLookup.Static, nonPublic);

            public static Reflection.Method<Action> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action>(methodName);

            public static Reflection.Method<Func<T, R>> Get<R>(string methodName, bool nonPublic = false)
                => Get<Func<T, R>>(methodName, MemberLookup.Instance, nonPublic);

            public static Reflection.Method<Func<T, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<R>>(methodName);
            
            public static Reflection.Method<Func<R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Get<Func<R>>(methodName, MemberLookup.Static, nonPublic);

            public static Reflection.Method<Func<R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<R>>(methodName);
        }
        
        /// <summary>
        /// Provides access to methods with single parameter declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P">Type of method parameter.</typeparam>
        public static class Method<P>
        {
            public static Reflection.Method<Action<T, P>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P>>(methodName, MemberLookup.Instance, nonPublic);

            public static Reflection.Method<Action<T, P>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P>>(methodName);

            public static Reflection.Method<Action<P>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P>>(methodName, MemberLookup.Static, nonPublic);

            public static Reflection.Method<Action<P>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P>>( methodName);
            
            public static Reflection.Method<Func<T, P, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P, R>>(methodName, MemberLookup.Instance, nonPublic);

            public static Reflection.Method<Func<T, P, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P>>(methodName);

            public static Reflection.Method<Func<P, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P, R>>(methodName, MemberLookup.Static, nonPublic);

            public static Reflection.Method<Func<P, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with two parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P1">Type of first method parameter.</typeparam>
        /// <typeparam name="P2">Type of second method parameter.</typeparam>
        public static class Method<P1, P2>
        {
            public static Reflection.Method<Action<T, P1, P2>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P1, P2>>(methodName, MemberLookup.Instance, nonPublic);

            public static Reflection.Method<Action<T, P1, P2>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2>>(methodName);

            public static Reflection.Method<Action<P1, P2>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P1, P2>>(methodName, MemberLookup.Static, nonPublic);

            public static Reflection.Method<Action<P1, P2>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2>>( methodName);
            
            public static Reflection.Method<Func<T, P1, P2, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P1, P2, R>>(methodName, MemberLookup.Instance, nonPublic);

            public static Reflection.Method<Func<T, P1, P2, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2>>(methodName);

            public static Reflection.Method<Func<P1, P2, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P1, P2, R>>(methodName, MemberLookup.Static, nonPublic);

            public static Reflection.Method<Func<P1, P2, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with three parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P1">Type of first method parameter.</typeparam>
        /// <typeparam name="P2">Type of second method parameter.</typeparam>
        /// <typeparam name="P3">Type of third method parameter.</typeparam>
        public static class Method<P1, P2, P3>
        {
            public static Reflection.Method<Action<T, P1, P2, P3>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P1, P2, P3>>(methodName, MemberLookup.Instance, nonPublic);

            public static Reflection.Method<Action<T, P1, P2, P3>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3>>(methodName);

            public static Reflection.Method<Action<P1, P2, P3>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P1, P2, P3>>(methodName, MemberLookup.Static, nonPublic);

            public static Reflection.Method<Action<P1, P2, P3>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3>>( methodName);
            
            public static Reflection.Method<Func<T, P1, P2, P3, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P1, P2, P3, R>>(methodName, MemberLookup.Instance, nonPublic);

            public static Reflection.Method<Func<T, P1, P2, P3, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3>>(methodName);

            public static Reflection.Method<Func<P1, P2, P3, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P1, P2, P3, R>>(methodName, MemberLookup.Static, nonPublic);

            public static Reflection.Method<Func<P1, P2, P3, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3>>(methodName);
        }
    }
}