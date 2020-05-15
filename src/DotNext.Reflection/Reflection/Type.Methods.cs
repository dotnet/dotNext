using System;
using Missing = System.Reflection.Missing;

namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Reflects static method declared in type <typeparamref name="T"/> which
        /// returns value of type <typeparamref name="TResult"/> and has arguments described
        /// by type <typeparamref name="TArgs"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Function{A, R}"/> which
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="TArgs">The value type describing arguments of the method.</typeparam>
        /// <typeparam name="TResult">The method return type.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/> if method doesn't exist.</returns>
        public static Reflection.Method<Function<TArgs, TResult>>? GetStaticMethod<TArgs, TResult>(string methodName, bool nonPublic = false)
            where TArgs : struct
            => Method.Get<Function<TArgs, TResult>>(methodName, MethodLookup.Static, nonPublic);

        /// <summary>
        /// Reflects static method declared in type <typeparamref name="T"/> which
        /// returns value of type <typeparamref name="TResult"/> and has arguments described
        /// by type <typeparamref name="TArgs"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Function{A, R}"/> which
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="TArgs">The value type describing arguments of the method.</typeparam>
        /// <typeparam name="TResult">The method return type.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        public static Reflection.Method<Function<TArgs, TResult>> RequireStaticMethod<TArgs, TResult>(string methodName, bool nonPublic = false)
            where TArgs : struct
            => GetStaticMethod<TArgs, TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, TArgs, TResult>(methodName);

        /// <summary>
        /// Reflects instance method declared in type <typeparamref name="T"/> which
        /// returns value of type <typeparamref name="TResult"/> and has arguments described
        /// by type <typeparamref name="TArgs"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Function{T, A, R}"/> which
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="TArgs">The value type describing arguments of the method.</typeparam>
        /// <typeparam name="TResult">The method return type.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/> if method doesn't exist.</returns>
        public static Reflection.Method<Function<T, TArgs, TResult>>? GetMethod<TArgs, TResult>(string methodName, bool nonPublic = false)
            where TArgs : struct
            => Method.Get<Function<T, TArgs, TResult>>(methodName, MethodLookup.Instance, nonPublic);

        /// <summary>
        /// Reflects instance method declared in type <typeparamref name="T"/> which
        /// returns value of type <typeparamref name="TResult"/> and has arguments described
        /// by type <typeparamref name="TArgs"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Function{T, A, R}"/> which
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="TArgs">The value type describing arguments of the method.</typeparam>
        /// <typeparam name="TResult">The method return type.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        public static Reflection.Method<Function<T, TArgs, TResult>> RequireMethod<TArgs, TResult>(string methodName, bool nonPublic = false)
            where TArgs : struct
            => GetMethod<TArgs, TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, TArgs, TResult>(methodName);

        /// <summary>
        /// Reflects static method declared in type <typeparamref name="T"/> without return value
        /// and has arguments described by type <typeparamref name="TArgs"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Procedure{A}"/> which
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="TArgs">The value type describing arguments of the method.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/> if method doesn't exist.</returns>
        public static Reflection.Method<Procedure<TArgs>>? GetStaticMethod<TArgs>(string methodName, bool nonPublic = false)
            where TArgs : struct
            => Method.Get<Procedure<TArgs>>(methodName, MethodLookup.Static, nonPublic);

        /// <summary>
        /// Reflects static method declared in type <typeparamref name="T"/> without return value
        /// and has arguments described by type <typeparamref name="TArgs"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Procedure{A}"/> which
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="TArgs">The value type describing arguments of the method.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/>.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        public static Reflection.Method<Procedure<TArgs>> RequireStaticMethod<TArgs>(string methodName, bool nonPublic = false)
            where TArgs : struct
            => GetStaticMethod<TArgs>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, TArgs, Missing>(methodName);

        /// <summary>
        /// Reflects instance method declared in type <typeparamref name="T"/> without return value
        /// and has arguments described by type <typeparamref name="TArgs"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Procedure{T, A}"/> which
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="TArgs">The value type describing arguments of the method.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/> if method doesn't exist.</returns>
        public static Reflection.Method<Procedure<T, TArgs>>? GetMethod<TArgs>(string methodName, bool nonPublic = false)
            where TArgs : struct
            => Method.Get<Procedure<T, TArgs>>(methodName, MethodLookup.Instance, nonPublic);

        /// <summary>
        /// Reflects instance method declared in type <typeparamref name="T"/> without return value
        /// and has arguments described by type <typeparamref name="TArgs"/>.
        /// </summary>
        /// <remarks>
        /// The reflected method is represented by universal delegate type <see cref="Procedure{T, A}"/> which
        /// allocates arguments on the stack instead of registry-based allocated or any other optimizations
        /// performed by .NET Runtime.
        /// </remarks>
        /// <typeparam name="TArgs">The value type describing arguments of the method.</typeparam>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
        /// <returns>The reflected method; or <see langword="null"/>.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        public static Reflection.Method<Procedure<T, TArgs>> RequireMethod<TArgs>(string methodName, bool nonPublic = false)
            where TArgs : struct
            => GetMethod<TArgs>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, TArgs, Missing>(methodName);

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
            /// <typeparam name="TSignature">The delegate describing signature of the requested method.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="methodType">The type of the method to be resolved.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<TSignature>? Get<TSignature>(string methodName, MethodLookup methodType, bool nonPublic = false)
                where TSignature : MulticastDelegate
                => Reflection.Method<TSignature>.GetOrCreate<T>(methodName, nonPublic, methodType);

            /// <summary>
            /// Reflects class method.
            /// </summary>
            /// <remarks>
            /// This method supports special types of delegates: <see cref="Function{T, A, R}"/> or <see cref="Procedure{T, A}"/> for instance methods,
            /// <see cref="Function{A, R}"/> or <see cref="Procedure{A}"/> for static methods.
            /// The value returned by this method is cached by the given delegate type and method name.
            /// Two calls of this method with the same arguments will return the same object.
            /// </remarks>
            /// <typeparam name="TSignature">The delegate describing signature of the requested method.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="methodType">The type of the method to be resolved.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<TSignature> Require<TSignature>(string methodName, MethodLookup methodType, bool nonPublic = false)
                where TSignature : MulticastDelegate
                => Get<TSignature>(methodName, methodType, nonPublic) ?? throw MissingMethodException.Create<T, TSignature>(methodName);

            /// <summary>
            /// Reflects instance parameterless method without return type as delegate type <see cref="Action{T}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T>>? Get(string methodName, bool nonPublic = false)
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
            public static Reflection.Method<Action>? GetStatic(string methodName, bool nonPublic = false)
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
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, TResult>>? Get<TResult>(string methodName, bool nonPublic = false)
                => Get<Func<T, TResult>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance parameterless method which as delegate type <see cref="Func{T, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, TResult>> Require<TResult>(string methodName, bool nonPublic = false)
                => Get<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<TResult>>(methodName);

            /// <summary>
            /// Reflects static parameterless method which as delegate type <see cref="Func{R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<TResult>>? GetStatic<TResult>(string methodName, bool nonPublic = false)
                => Get<Func<TResult>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static parameterless method which as delegate type <see cref="Func{R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<TResult>> RequireStatic<TResult>(string methodName, bool nonPublic = false)
                => GetStatic<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<TResult>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with single parameter declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TParam">Type of method parameter.</typeparam>
        public static class Method<TParam>
        {
            /// <summary>
            /// Reflects instance method with single parameter and without return type as delegate type <see cref="Action{T, P}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, TParam>>? Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, TParam>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with single parameter and without return type as delegate type <see cref="Action{T, P}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, TParam>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<TParam>>(methodName);

            /// <summary>
            /// Reflects static method with single parameter and without return type as delegate type <see cref="Action{P}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<TParam>>? GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<TParam>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with single parameter and without return type as delegate type <see cref="Action{P}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<TParam>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<TParam>>(methodName);

            /// <summary>
            /// Reflects instance method with single parameter as delegate type <see cref="Func{T, P, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, TParam, TResult>>? Get<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, TParam, TResult>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with single parameter as delegate type <see cref="Func{T, P, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, TParam, TResult>> Require<TResult>(string methodName, bool nonPublic = false)
                => Get<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<TParam, TResult>>(methodName);

            /// <summary>
            /// Reflects static method with single parameter as delegate type <see cref="Func{P, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<TParam, TResult>>? GetStatic<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<TParam, TResult>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with single parameter as delegate type <see cref="Func{P, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<TParam, TResult>> RequireStatic<TResult>(string methodName, bool nonPublic = false)
                => GetStatic<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<TParam, TResult>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with two parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T1">Type of first method parameter.</typeparam>
        /// <typeparam name="T2">Type of second method parameter.</typeparam>
        public static class Method<T1, T2>
        {
            /// <summary>
            /// Reflects instance method with two parameters and without return type as delegate type <see cref="Action{T, P1, P2}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, T1, T2>>? Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, T1, T2>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with two parameters and without return type as delegate type <see cref="Action{T, P1, P2}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, T1, T2>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2>>(methodName);

            /// <summary>
            /// Reflects static method with two parameters and without return type as delegate type <see cref="Action{P1, P2}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T1, T2>>? GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<T1, T2>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with two parameters and without return type as delegate type <see cref="Action{P1, P2}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T1, T2>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2>>(methodName);

            /// <summary>
            /// Reflects instance method with two parameters as delegate type <see cref="Func{T, P1, P2, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, T1, T2, TResult>>? Get<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, T1, T2, TResult>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with two parameters as delegate type <see cref="Func{T, P1, P2, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, T1, T2, TResult>> Require<TResult>(string methodName, bool nonPublic = false)
                => Get<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, TResult>>(methodName);

            /// <summary>
            /// Reflects static method with two parameters as delegate type <see cref="Func{P1, P2, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T1, T2, TResult>>? GetStatic<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T1, T2, TResult>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with two parameters as delegate type <see cref="Func{P1, P2, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T1, T2, TResult>> RequireStatic<TResult>(string methodName, bool nonPublic = false)
                => GetStatic<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, TResult>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with three parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T1">Type of first method parameter.</typeparam>
        /// <typeparam name="T2">Type of second method parameter.</typeparam>
        /// <typeparam name="T3">Type of third method parameter.</typeparam>
        public static class Method<T1, T2, T3>
        {
            /// <summary>
            /// Reflects instance method with three parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, T1, T2, T3>>? Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, T1, T2, T3>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with three parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, T1, T2, T3>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2, T3>>(methodName);

            /// <summary>
            /// Reflects static method with three parameters and without return type as delegate type <see cref="Action{P1, P2, P3}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T1, T2, T3>>? GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<T1, T2, T3>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with three parameters and without return type as delegate type <see cref="Action{P1, P2, P3}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T1, T2, T3>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2, T3>>(methodName);

            /// <summary>
            /// Reflects instance method with three parameters as delegate type <see cref="Func{T, P1, P2, P3, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, T1, T2, T3, TResult>>? Get<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, T1, T2, T3, TResult>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with three parameters as delegate type <see cref="Func{T, P1, P2, P3, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, T1, T2, T3, TResult>> Require<TResult>(string methodName, bool nonPublic = false)
                => Get<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, T3, TResult>>(methodName);

            /// <summary>
            /// Reflects static method with three parameters as delegate type <see cref="Func{P1, P2, P3, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T1, T2, T3, TResult>>? GetStatic<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T1, T2, T3, TResult>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with three parameters as delegate type <see cref="Func{P1, P2, P3, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T1, T2, T3, TResult>> RequireStatic<TResult>(string methodName, bool nonPublic = false)
                => GetStatic<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, T3, TResult>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with four parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T1">Type of first method parameter.</typeparam>
        /// <typeparam name="T2">Type of second method parameter.</typeparam>
        /// <typeparam name="T3">Type of third method parameter.</typeparam>
        /// <typeparam name="T4">Type of fourth method parameter.</typeparam>
        public static class Method<T1, T2, T3, T4>
        {
            /// <summary>
            /// Reflects instance method with four parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, T1, T2, T3, T4>>? Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, T1, T2, T3, T4>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with four parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, T1, T2, T3, T4>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2, T3, T4>>(methodName);

            /// <summary>
            /// Reflects static method with four parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T1, T2, T3, T4>>? GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<T1, T2, T3, T4>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with four parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T1, T2, T3, T4>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2, T3, T4>>(methodName);

            /// <summary>
            /// Reflects instance method with four parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, T1, T2, T3, T4, TResult>>? Get<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, T1, T2, T3, T4, TResult>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with four parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, T1, T2, T3, T4, TResult>> Require<TResult>(string methodName, bool nonPublic = false)
                => Get<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, T3, T4, TResult>>(methodName);

            /// <summary>
            /// Reflects static method with four parameters as delegate type <see cref="Func{P1, P2, P3, P4, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T1, T2, T3, T4, TResult>>? GetStatic<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T1, T2, T3, T4, TResult>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with four parameters as delegate type <see cref="Func{P1, P2, P3, P4, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T1, T2, T3, T4, TResult>> RequireStatic<TResult>(string methodName, bool nonPublic = false)
                => GetStatic<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, T3, T4, TResult>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with five parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T1">Type of first method parameter.</typeparam>
        /// <typeparam name="T2">Type of second method parameter.</typeparam>
        /// <typeparam name="T3">Type of third method parameter.</typeparam>
        /// <typeparam name="T4">Type of fourth method parameter.</typeparam>
        /// <typeparam name="T5">Type of fifth method parameter.</typeparam>
        public static class Method<T1, T2, T3, T4, T5>
        {
            /// <summary>
            /// Reflects instance method with five parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4, P5}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, T1, T2, T3, T4, T5>>? Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, T1, T2, T3, T4, T5>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with five parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4, P5}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, T1, T2, T3, T4, T5>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2, T3, T4, T5>>(methodName);

            /// <summary>
            /// Reflects static method with five parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4, P5}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T1, T2, T3, T4, T5>>? GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<T1, T2, T3, T4, T5>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with five parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4, P5}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T1, T2, T3, T4, T5>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2, T3, T4, T5>>(methodName);

            /// <summary>
            /// Reflects instance method with five parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, P5, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, T1, T2, T3, T4, T5, TResult>>? Get<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, T1, T2, T3, T4, T5, TResult>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with five parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, P5, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, T1, T2, T3, T4, T5, TResult>> Require<TResult>(string methodName, bool nonPublic = false)
                => Get<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, T3, T4, T5, TResult>>(methodName);

            /// <summary>
            /// Reflects static method with five parameters as delegate type <see cref="Func{P1, P2, P3, P4, P5, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T1, T2, T3, T4, T5, TResult>>? GetStatic<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T1, T2, T3, T4, T5, TResult>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with five parameters as delegate type <see cref="Func{P1, P2, P3, P4, P5, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T1, T2, T3, T4, T5, TResult>> RequireStatic<TResult>(string methodName, bool nonPublic = false)
                => GetStatic<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, T3, T4, T5, TResult>>(methodName);
        }

        /// <summary>
        /// Provides access to methods with six parameters declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T1">Type of first method parameter.</typeparam>
        /// <typeparam name="T2">Type of second method parameter.</typeparam>
        /// <typeparam name="T3">Type of third method parameter.</typeparam>
        /// <typeparam name="T4">Type of fourth method parameter.</typeparam>
        /// <typeparam name="T5">Type of fifth method parameter.</typeparam>
        /// <typeparam name="T6">Type of sixth method parameter.</typeparam>
        public static class Method<T1, T2, T3, T4, T5, T6>
        {
            /// <summary>
            /// Reflects instance method with six parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4, P5, P6}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T, T1, T2, T3, T4, T5, T6>>? Get(string methodName, bool nonPublic = false)
                => Method.Get<Action<T, T1, T2, T3, T4, T5, T6>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with six parameters and without return type as delegate type <see cref="Action{T, P1, P2, P3, P4, P5, P6}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T, T1, T2, T3, T4, T5, T6>> Require(string methodName, bool nonPublic = false)
                => Get(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2, T3, T4, T5, T6>>(methodName);

            /// <summary>
            /// Reflects static method with six parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4, P5, P6}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Action<T1, T2, T3, T4, T5, T6>>? GetStatic(string methodName, bool nonPublic = false)
                => Method.Get<Action<T1, T2, T3, T4, T5, T6>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with six parameters and without return type as delegate type <see cref="Action{P1, P2, P3, P4, P5, P6}"/>.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Action<T1, T2, T3, T4, T5, T6>> RequireStatic(string methodName, bool nonPublic = false)
                => GetStatic(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Action<T1, T2, T3, T4, T5, T6>>(methodName);

            /// <summary>
            /// Reflects instance method with six parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, P5, P6, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T, T1, T2, T3, T4, T5, T6, TResult>>? Get<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T, T1, T2, T3, T4, T5, T6, TResult>>(methodName, MethodLookup.Instance, nonPublic);

            /// <summary>
            /// Reflects instance method with six parameters as delegate type <see cref="Func{T, P1, P2, P3, P4, P5, P6, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T, T1, T2, T3, T4, T5, T6, TResult>> Require<TResult>(string methodName, bool nonPublic = false)
                => Get<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, T3, T4, T5, T6, TResult>>(methodName);

            /// <summary>
            /// Reflects static method with six parameters as delegate type <see cref="Func{P1, P2, P3, P4, P5, P6, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method; otherwise, <see langword="null"/> if method doesn't exist.</returns>
            public static Reflection.Method<Func<T1, T2, T3, T4, T5, T6, TResult>>? GetStatic<TResult>(string methodName, bool nonPublic = false)
                => Method.Get<Func<T1, T2, T3, T4, T5, T6, TResult>>(methodName, MethodLookup.Static, nonPublic);

            /// <summary>
            /// Reflects static method with six parameters as delegate type <see cref="Func{P1, P2, P3, P4, P5, P6, R}"/>.
            /// </summary>
            /// <typeparam name="TResult">The method return type.</typeparam>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public method.</param>
            /// <returns>The reflected method.</returns>
            /// <exception cref="MissingMethodException">The requested method doesn't exist.</exception>
            public static Reflection.Method<Func<T1, T2, T3, T4, T5, T6, TResult>> RequireStatic<TResult>(string methodName, bool nonPublic = false)
                => GetStatic<TResult>(methodName, nonPublic) ?? throw MissingMethodException.Create<T, Func<T1, T2, T3, T4, T5, T6, TResult>>(methodName);
        }
    }
}