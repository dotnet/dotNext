using System;
using Missing = System.Reflection.Missing;

namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Reflects static method declared in type <typeparamref name="T"/> which
        /// returns value of type <typeparamref name="R"/> and has arguments described
        /// by type <typeparamref name="A"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Function{A, R}"/> which 
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="A">The value type describing arguments of the method.</typeparam>
        /// <typeparam name="R">The method return type.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/> if method doesn't exist.</returns>
        public static Reflection.Method<Function<A, R>> GetStaticMethod<A, R>(string methodName, bool nonPublic = false)
            where A : struct
            => Method.Get<Function<A, R>>(methodName, MethodLookup.Static, nonPublic);

        /// <summary>
        /// Reflects static method declared in type <typeparamref name="T"/> which
        /// returns value of type <typeparamref name="R"/> and has arguments described
        /// by type <typeparamref name="A"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Function{A, R}"/> which 
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="A">The value type describing arguments of the method.</typeparam>
        /// <typeparam name="R">The method return type.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        public static Reflection.Method<Function<A, R>> RequireStaticMethod<A, R>(string methodName, bool nonPublic = false)
            where A : struct
            => GetStaticMethod<A, R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, A, R>(methodName);

        /// <summary>
        /// Reflects instance method declared in type <typeparamref name="T"/> which
        /// returns value of type <typeparamref name="R"/> and has arguments described
        /// by type <typeparamref name="A"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Function{T, A, R}"/> which 
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="A">The value type describing arguments of the method.</typeparam>
        /// <typeparam name="R">The method return type.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/> if method doesn't exist.</returns>
        public static Reflection.Method<Function<T, A, R>> GetMethod<A, R>(string methodName, bool nonPublic = false)
            where A : struct
            => Method.Get<Function<T, A, R>>(methodName, MethodLookup.Instance, nonPublic);

        /// <summary>
        /// Reflects instance method declared in type <typeparamref name="T"/> which
        /// returns value of type <typeparamref name="R"/> and has arguments described
        /// by type <typeparamref name="A"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Function{T, A, R}"/> which 
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="A">The value type describing arguments of the method.</typeparam>
        /// <typeparam name="R">The method return type.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        public static Reflection.Method<Function<T, A, R>> RequireMethod<A, R>(string methodName, bool nonPublic = false)
            where A : struct
            => GetMethod<A, R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, A, R>(methodName);

        /// <summary>
        /// Reflects static method declared in type <typeparamref name="T"/> without return value 
        /// and has arguments described by type <typeparamref name="A"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Procedure{A}"/> which 
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="A">The value type describing arguments of the method.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/> if method doesn't exist.</returns>
        public static Reflection.Method<Procedure<A>> GetStaticMethod<A>(string methodName, bool nonPublic = false)
            where A : struct
            => Method.Get<Procedure<A>>(methodName, MethodLookup.Static, nonPublic);

        /// <summary>
        /// Reflects static method declared in type <typeparamref name="T"/> without return value 
        /// and has arguments described by type <typeparamref name="A"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Procedure{A}"/> which 
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="A">The value type describing arguments of the method.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/>.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        public static Reflection.Method<Procedure<A>> RequireStaticMethod<A>(string methodName, bool nonPublic = false)
            where A : struct
            => GetStaticMethod<A>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, A, Missing>(methodName);

        /// <summary>
        /// Reflects instance method declared in type <typeparamref name="T"/> without return value 
        /// and has arguments described by type <typeparamref name="A"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Procedure{T, A}"/> which 
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="A">The value type describing arguments of the method.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/> if method doesn't exist.</returns>
        public static Reflection.Method<Procedure<T, A>> GetMethod<A>(string methodName, bool nonPublic = false)
            where A : struct
            => Method.Get<Procedure<T, A>>(methodName, MethodLookup.Instance, nonPublic);

        /// <summary>
        /// Reflects instance method declared in type <typeparamref name="T"/> without return value 
        /// and has arguments described by type <typeparamref name="A"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Procedure{T, A}"/> which 
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="A">The value type describing arguments of the method.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/>.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        public static Reflection.Method<Procedure<T, A>> RequireMethod<A>(string methodName, bool nonPublic = false)
            where A : struct
            => GetMethod<A>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, A, Missing>(methodName);

        /// <summary>
        /// Provides access to methods declared in type <typeparamref name="T"/>.
        /// </summary>
        public static class Method
        {
            /// <summary>
            /// Reflects class method.
            /// </summary>
            /// <remarks>
            /// This method supports special types of delegates: <see cref="Function{T, A, R}"/> or <see cref="Procedure{T, A}"/> for instance methods,
            /// <see cref="Function{A, R}"/> or <see cref="Procedure{A}"/> for static methods.
            /// The value returned by this method is cached by the given delegate type and method name.
            /// Two calls of this method with the same arguments will return the same object.
            /// </remarks>
            /// <typeparam name="D">The delegate describing signature of the requested method.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="methodType">The type of the method to be resolved.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<D> Get<D>(string methodName, MethodLookup methodType, bool nonPublic = false)
                where D : MulticastDelegate
                => Reflection.Method<D>.GetOrCreate<T>(methodName, nonPublic, methodType);

            /// <summary>
            /// Reflects class method.
            /// </summary>
            /// <remarks>
            /// This method supports special types of delegates: <see cref="Function{T, A, R}"/> or <see cref="Procedure{T, A}"/> for instance methods,
            /// <see cref="Function{A, R}"/> or <see cref="Procedure{A}"/> for static methods.
            /// The value returned by this method is cached by the given delegate type and method name.
            /// Two calls of this method with the same arguments will return the same object.
            /// </remarks>
            /// <typeparam name="D">The delegate describing signature of the requested method.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="methodType">The type of the method to be resolved.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<D> Require<D>(string methodName, MethodLookup methodType, bool nonPublic = false)
                where D : MulticastDelegate
                => Get<D>(methodName, methodType, nonPublic) ?? throw MissingMethodException.Create<T, D>(methodName);

            /// <summary>
            /// Reflects instance parameterless method without return type as delegate type <see cref="Action{T}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T>> Get(string methodName, bool nonPublic = false)
                => Get<Action<T>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance parameterless method without return type as delegate type <see cref="Action{T}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action>(methodName);

            /// <summary>
            /// Reflects static parameterless method without return type as delegate type <see cref="Action"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action> GetStatic(string methodName, bool nonPublic = false)
                => Get<Action>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static parameterless method without return type as delegate type <see cref="Action"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action>(methodName);

            /// <summary>
            /// Reflects instance parameterless method which as delegate type <see cref="Func{T, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, R>> Get<R>(string methodName, bool nonPublic = false)
                => Get<Func<T, R>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance parameterless method which as delegate type <see cref="Func{T, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<R>>(methodName);

            /// <summary>
            /// Reflects static parameterless method which as delegate type <see cref="Func{R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Get<Func<R>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static parameterless method which as delegate type <see cref="Func{R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<R>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with single parameter declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P">Type of method parameter.</typeparam>
        public static class Method<P>
        {
            /// <summary>
            /// Reflects instance method with single parameter and without return type as delegate type <see cref="Action{T, P}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, P>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with single parameter and without return type as delegate type <see cref="Action{T, P}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, P>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P>>(methodName);

            /// <summary>
            /// Reflects static method with single parameter and without return type as delegate type <see cref="Action{P}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<P>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with single parameter and without return type as delegate type <see cref="Action{P}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<P>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P>>(methodName);

            /// <summary>
            /// Reflects instance method with single parameter as delegate type <see cref="Func{T, P, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, P, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P, R>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with single parameter as delegate type <see cref="Func{T, P, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, P, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P, R>>(methodName);

            /// <summary>
            /// Reflects static method with single parameter as delegate type <see cref="Func{P, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<P, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P, R>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with single parameter as delegate type <see cref="Func{P, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<P, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P, R>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with two parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P1">Type of first method parameter.</typeparam>
        /// <typeparam name="P2">Type of second method parameter.</typeparam>
        public static class Method<P1, P2>
        {
            /// <summary>
            /// Reflects instance method with two parameters and without return type as delegate type <see cref="Action{T, P1, P2}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, P1, P2>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P1, P2>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with two parameters and without return type as delegate type <see cref="Action{T, P1, P2}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, P1, P2>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2>>(methodName);

            /// <summary>
            /// Reflects static method with two parameters and without return type as delegate type <see cref="Action{P1, P2}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<P1, P2>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P1, P2>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with two parameters and without return type as delegate type <see cref="Action{P1, P2}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<P1, P2>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2>>(methodName);

            /// <summary>
            /// Reflects instance method with two parameters as delegate type <see cref="Func{T, P1, P2, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, P1, P2, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P1, P2, R>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with two parameters as delegate type <see cref="Func{T, P1, P2, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, P1, P2, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, R>>(methodName);

            /// <summary>
            /// Reflects static method with two parameters as delegate type <see cref="Func{P1, P2, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<P1, P2, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P1, P2, R>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with two parameters as delegate type <see cref="Func{P1, P2, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<P1, P2, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, R>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with three parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P1">Type of first method parameter.</typeparam>
        /// <typeparam name="P2">Type of second method parameter.</typeparam>
        /// <typeparam name="P3">Type of third method parameter.</typeparam>
        public static class Method<P1, P2, P3>
        {
            /// <summary>
            /// Reflects instance method with three parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, P1, P2, P3>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P1, P2, P3>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with three parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, P1, P2, P3>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3>>(methodName);

            /// <summary>
            /// Reflects static method with three parameters and without return type as delegate type <see cref="Action{P1, P2, P3}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<P1, P2, P3>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P1, P2, P3>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with three parameters and without return type as delegate type <see cref="Action{P1, P2, P3}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<P1, P2, P3>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3>>(methodName);

            /// <summary>
            /// Reflects instance method with three parameters as delegate type <see cref="Func{T, P1, P2, P3, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, P1, P2, P3, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P1, P2, P3, R>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with three parameters as delegate type <see cref="Func{T, P1, P2, P3, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, P1, P2, P3, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, P3, R>>(methodName);

            /// <summary>
            /// Reflects static method with three parameters as delegate type <see cref="Func{P1, P2, P3, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<P1, P2, P3, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P1, P2, P3, R>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with three parameters as delegate type <see cref="Func{P1, P2, P3, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<P1, P2, P3, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, P3, R>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with four parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P1">Type of first method parameter.</typeparam>
        /// <typeparam name="P2">Type of second method parameter.</typeparam>
        /// <typeparam name="P3">Type of third method parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth method parameter.</typeparam>
        public static class Method<P1, P2, P3, P4>
        {
            /// <summary>
            /// Reflects instance method with four parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, P1, P2, P3, P4>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P1, P2, P3, P4>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with four parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, P1, P2, P3, P4>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3, P4>>(methodName);

            /// <summary>
            /// Reflects static method with four parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<P1, P2, P3, P4>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P1, P2, P3, P4>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with four parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<P1, P2, P3, P4>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3, P4>>(methodName);

            /// <summary>
            /// Reflects instance method with four parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, P1, P2, P3, P4, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P1, P2, P3, P4, R>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with four parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, P1, P2, P3, P4, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, P3, P4, R>>(methodName);

            /// <summary>
            /// Reflects static method with four parameters as delegate type <see cref="Func{P1, P2, P3, P4, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<P1, P2, P3, P4, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P1, P2, P3, P4, R>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with four parameters as delegate type <see cref="Func{P1, P2, P3, P4, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<P1, P2, P3, P4, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, P3, P4, R>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with five parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P1">Type of first method parameter.</typeparam>
        /// <typeparam name="P2">Type of second method parameter.</typeparam>
        /// <typeparam name="P3">Type of third method parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth method parameter.</typeparam>
        /// <typeparam name="P5">Type of fifth method parameter.</typeparam>
        public static class Method<P1, P2, P3, P4, P5>
        {
            /// <summary>
            /// Reflects instance method with five parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4, P5}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, P1, P2, P3, P4, P5>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P1, P2, P3, P4, P5>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with five parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4, P5}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, P1, P2, P3, P4, P5>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3, P4, P5>>(methodName);

            /// <summary>
            /// Reflects static method with five parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4, P5}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<P1, P2, P3, P4, P5>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P1, P2, P3, P4, P5>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with five parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4, P5}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<P1, P2, P3, P4, P5>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3, P4, P5>>(methodName);

            /// <summary>
            /// Reflects instance method with five parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, P5, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, P1, P2, P3, P4, P5, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P1, P2, P3, P4, P5, R>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with five parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, P5, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, P1, P2, P3, P4, P5, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, P3, P4, P5, R>>(methodName);

            /// <summary>
            /// Reflects static method with five parameters as delegate type <see cref="Func{P1, P2, P3, P4, P5, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<P1, P2, P3, P4, P5, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P1, P2, P3, P4, P5, R>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with five parameters as delegate type <see cref="Func{P1, P2, P3, P4, P5, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<P1, P2, P3, P4, P5, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, P3, P4, P5, R>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with six parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P1">Type of first method parameter.</typeparam>
        /// <typeparam name="P2">Type of second method parameter.</typeparam>
        /// <typeparam name="P3">Type of third method parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth method parameter.</typeparam>
        /// <typeparam name="P5">Type of fifth method parameter.</typeparam>
        /// <typeparam name="P6">Type of sixth method parameter.</typeparam>
        public static class Method<P1, P2, P3, P4, P5, P6>
        {
            /// <summary>
            /// Reflects instance method with six parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4, P5, P6}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, P1, P2, P3, P4, P5, P6>> Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, P1, P2, P3, P4, P5, P6>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with six parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4, P5, P6}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, P1, P2, P3, P4, P5, P6>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3, P4, P5, P6>>(methodName);

            /// <summary>
            /// Reflects static method with six parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4, P5, P6}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<P1, P2, P3, P4, P5, P6>> GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<P1, P2, P3, P4, P5, P6>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with six parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4, P5, P6}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<P1, P2, P3, P4, P5, P6>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<P1, P2, P3, P4, P5, P6>>(methodName);

            /// <summary>
            /// Reflects instance method with six parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, P5, P6, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, P1, P2, P3, P4, P5, P6, R>> Get<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, P1, P2, P3, P4, P5, P6, R>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with six parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, P5, P6, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, P1, P2, P3, P4, P5, P6, R>> Require<R>(string methodName, bool nonPublic = false)
                => Get<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, P3, P4, P5, P6, R>>(methodName);

            /// <summary>
            /// Reflects static method with six parameters as delegate type <see cref="Func{P1, P2, P3, P4, P5, P6, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<P1, P2, P3, P4, P5, P6, R>> GetStatic<R>(string methodName, bool nonPublic = false)
                => Method.Get<Func<P1, P2, P3, P4, P5, P6, R>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with six parameters as delegate type <see cref="Func{P1, P2, P3, P4, P5, P6, R}"/>.
            /// </summary>
            /// <typeparam name="R">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<P1, P2, P3, P4, P5, P6, R>> RequireStatic<R>(string methodName, bool nonPublic = false)
                => GetStatic<R>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<P1, P2, P3, P4, P5, P6, R>>(methodName);
        }
    }
}