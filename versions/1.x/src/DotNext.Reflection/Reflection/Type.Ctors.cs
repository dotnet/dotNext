using System;
using DefaultMemberAttribute = System.Reflection.DefaultMemberAttribute;

namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Reflects constructor as function.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <typeparam name="A">A structure describing constructor signature.</typeparam>
        /// <returns>Constructor for type <typeparamref name="T"/>; or null, if it doesn't exist.</returns>
        public static Reflection.Constructor<Function<A, T>> GetConstructor<A>(bool nonPublic = false)
            where A : struct
            => Constructor.Get<Function<A, T>>(nonPublic);

        /// <summary>
        /// Reflects constructor as function.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <typeparam name="A">A structure describing constructor signature.</typeparam>
        /// <returns>Constructor for type <typeparamref name="T"/>.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Function<A, T>> RequireConstructor<A>(bool nonPublic = false)
            where A : struct
            => GetConstructor<A>(nonPublic) ?? throw MissingConstructorException.Create<T, A>();

        /// <summary>
        /// Creates a new instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="args">The structure containing arguments to be passed into constructor.</param>
        /// <param name="nonPublic">True to reflect non-public constructor.</param>
        /// <typeparam name="A">A structure describing constructor signature.</typeparam>
        /// <returns>A new instance of type <typeparamref name="T"/>.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static T NewInstance<A>(in A args, bool nonPublic = false)
            where A : struct
             => RequireConstructor<A>(nonPublic).Invoke(args);

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> without parameters.
        /// </summary>
        [DefaultMember("Invoke")]
        public static class Constructor
        {
            /// <summary>
            /// Reflects constructor of type <typeparamref name="T"/> which signature
            /// is specified by delegate type.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <typeparam name="D">Type of delegate describing constructor signature.</typeparam>
            /// <returns>Reflected constructor; or <see langword="null"/>, if constructor doesn't exist.</returns>
            public static Reflection.Constructor<D> Get<D>(bool nonPublic = false)
                where D : MulticastDelegate
                => Reflection.Constructor<D>.GetOrCreate<T>(nonPublic);

            /// <summary>
            /// Reflects constructor of type <typeparamref name="T"/> which signature
            /// is specified by delegate type.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <typeparam name="D">Type of delegate describing constructor signature.</typeparam>
            /// <returns>Reflected constructor.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<D> Require<D>(bool nonPublic = false)
                where D : MulticastDelegate
                => Get<D>(nonPublic) ?? throw MissingConstructorException.Create<D>();

            /// <summary>
            /// Returns public constructor of type <typeparamref name="T"/> without parameters.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor without parameters; or <see langword="null"/>, if it doesn't exist.</returns>
            public static Reflection.Constructor<Func<T>> Get(bool nonPublic = false)
                => Get<Func<T>>(nonPublic);

            /// <summary>
            /// Returns public constructor of type <typeparamref name="T"/> without parameters.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor without parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(bool nonPublic = false)
            {
                Func<T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor();
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(bool nonPublic = false) => Require(nonPublic).Invoke();
        }

        /// <summary>
		/// Provides access to constructor of type <typeparamref name="T"/> with single parameter.
		/// </summary>
        /// <typeparam name="P">Type of constructor parameter.</typeparam>
        [DefaultMember("Invoke")]
        public static class Constructor<P>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with single parameter of type <typeparamref name="P"/>.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with single parameter; or <see langword="null"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with single parameter of type <typeparamref name="P"/>.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with single parameter.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg">Constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P arg, bool nonPublic = false)
            {
                Func<P, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg">Constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P arg, bool nonPublic = false) => Require(nonPublic).Invoke(arg);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with two parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with two 
			/// parameters of type <typeparamref name="P1"/> and <typeparamref name="P2"/>.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with two parameters; or <see langword="null"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with two 
            /// parameters of type <typeparamref name="P1"/> and <typeparamref name="P2"/>.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with two parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, bool nonPublic = false)
            {
                Func<P1, P2, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, bool nonPublic = false) => Require(nonPublic).Invoke(arg1, arg2);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with three parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2, P3>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with three 
			/// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/> and <typeparamref name="P3"/>.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with three parameters; or <see langword="null"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, P3, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, P3, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with three 
            /// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/> and <typeparamref name="P3"/>.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with three parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, P3, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2, P3>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, P3 arg3, bool nonPublic = false)
            {
                Func<P1, P2, P3, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2, arg3);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, P3 arg3, bool nonPublic = false) => Require(nonPublic).Invoke(arg1, arg2, arg3);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with four parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth constructor parameter.</typeparam>      
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2, P3, P4>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with four 
			/// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/>, <typeparamref name="P3"/> and <typeparamref name="P4"/>.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with four parameters; or <see langword="null"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, P3, P4, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, P3, P4, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with four 
            /// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/>, <typeparamref name="P3"/> and <typeparamref name="P4"/>.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with four parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, P3, P4, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2, P3, P4>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, bool nonPublic = false)
            {
                Func<P1, P2, P3, P4, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2, arg3, arg4);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, bool nonPublic = false) => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with five parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth constructor parameter.</typeparam>
        /// <typeparam name="P5">Type of fifth constructor parameter.</typeparam>             
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2, P3, P4, P5>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with five 
			/// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/>, 
			/// <typeparamref name="P3"/>, <typeparamref name="P4"/> and <typeparamref name="P5"/>.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with five parameters; or null, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, P3, P4, P5, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with five 
            /// parameters of type <typeparamref name="P1"/>, <typeparamref name="P2"/>, 
            /// <typeparamref name="P3"/>, <typeparamref name="P4"/> and <typeparamref name="P5"/>.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with five parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2, P3, P4, P5>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, bool nonPublic = false)
            {
                Func<P1, P2, P3, P4, P5, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2, arg3, arg4, arg5);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, bool nonPublic = false)
                => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with six parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth constructor parameter.</typeparam>
        /// <typeparam name="P5">Type of fifth constructor parameter.</typeparam> 
        /// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>              
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2, P3, P4, P5, P6>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with six parameters.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with six parameters; or <see langword="null"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, P3, P4, P5, P6, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with six parameters.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with six parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2, P3, P4, P5, P6>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, bool nonPublic = false)
            {
                Func<P1, P2, P3, P4, P5, P6, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2, arg3, arg4, arg5, arg6);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, bool nonPublic = false)
                => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with seven parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth constructor parameter.</typeparam>
        /// <typeparam name="P5">Type of fifth constructor parameter.</typeparam> 
        /// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>      
        /// <typeparam name="P7">Type of sixth constructor parameter.</typeparam>         
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2, P3, P4, P5, P6, P7>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with seven parameters.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with seven parameters; or <see langword="null"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, P7, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, P3, P4, P5, P6, P7, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with seven parameters.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with seven parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, P7, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2, P3, P4, P5, P6, P7>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="arg7">Seventh constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, bool nonPublic = false)
            {
                Func<P1, P2, P3, P4, P5, P6, P7, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="arg7">Seventh constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, bool nonPublic = false)
                => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with eight parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth constructor parameter.</typeparam>
        /// <typeparam name="P5">Type of fifth constructor parameter.</typeparam> 
        /// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>      
        /// <typeparam name="P7">Type of sixth constructor parameter.</typeparam>  
        /// <typeparam name="P8">Type of eighth constructor parameter.</typeparam>        
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2, P3, P4, P5, P6, P7, P8>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with eight parameters.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with eight parameters; or <see langword="null"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, P3, P4, P5, P6, P7, P8, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with eight parameters.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with eight parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2, P3, P4, P5, P6, P7, P8>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="arg7">Seventh constructor argument.</param>
            /// <param name="arg8">Eighth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, bool nonPublic = false)
            {
                Func<P1, P2, P3, P4, P5, P6, P7, P8, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="arg7">Seventh constructor argument.</param>
            /// <param name="arg8">Eighth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, bool nonPublic = false)
                => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with nine parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth constructor parameter.</typeparam>
        /// <typeparam name="P5">Type of fifth constructor parameter.</typeparam> 
        /// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>      
        /// <typeparam name="P7">Type of sixth constructor parameter.</typeparam>  
        /// <typeparam name="P8">Type of eighth constructor parameter.</typeparam>  
        /// <typeparam name="P9">Type of ninth constructor parameter.</typeparam>       
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2, P3, P4, P5, P6, P7, P8, P9>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with nine parameters.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with nine parameters; or <see langword="true"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with nine parameters.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with nine parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="arg7">Seventh constructor argument.</param>
            /// <param name="arg8">Eighth constructor argument.</param>
            /// <param name="arg9">Ninth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, bool nonPublic = false)
            {
                Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="arg7">Seventh constructor argument.</param>
            /// <param name="arg8">Eighth constructor argument.</param>
            /// <param name="arg9">Ninth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, bool nonPublic = false)
                => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        /// <summary>
        /// Provides access to constructor of type <typeparamref name="T"/> with nine parameters.
        /// </summary>
        /// <typeparam name="P1">Type of first constructor parameter.</typeparam>
		/// <typeparam name="P2">Type of second constructor parameter.</typeparam>
		/// <typeparam name="P3">Type of third constructor parameter.</typeparam>
        /// <typeparam name="P4">Type of fourth constructor parameter.</typeparam>
        /// <typeparam name="P5">Type of fifth constructor parameter.</typeparam> 
        /// <typeparam name="P6">Type of sixth constructor parameter.</typeparam>      
        /// <typeparam name="P7">Type of sixth constructor parameter.</typeparam>  
        /// <typeparam name="P8">Type of eighth constructor parameter.</typeparam>  
        /// <typeparam name="P9">Type of ninth constructor parameter.</typeparam>  
        /// <typeparam name="P10">Type of tenth constructor parameter.</typeparam>      
        [DefaultMember("Invoke")]
        public static class Constructor<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>
        {
            /// <summary>
			/// Returns constructor <typeparamref name="T"/> with ten parameters.
			/// </summary>
			/// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
			/// <returns>Reflected constructor with ten parameters; or <see langword="null"/>, if it doesn't exist.</returns>
			public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, T>> Get(bool nonPublic = false)
                => Constructor.Get<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, T>>(nonPublic);

            /// <summary>
            /// Returns constructor <typeparamref name="T"/> with ten parameters.
            /// </summary>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Reflected constructor with ten parameters.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static Reflection.Constructor<Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, T>> Require(bool nonPublic = false)
                => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>>();

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="arg7">Seventh constructor argument.</param>
            /// <param name="arg8">Eighth constructor argument.</param>
            /// <param name="arg9">Ninth constructor argument.</param>
            /// <param name="arg10">Tenth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
            public static Optional<T> TryInvoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10, bool nonPublic = false)
            {
                Func<P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, T> ctor = Get(nonPublic);
                return ctor is null ? Optional<T>.Empty : ctor(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
            }

            /// <summary>
            /// Invokes constructor.
            /// </summary>
            /// <param name="arg1">First constructor argument.</param>
            /// <param name="arg2">Second constructor argument.</param>
            /// <param name="arg3">Third constructor argument.</param>
            /// <param name="arg4">Fourth constructor argument.</param>
            /// <param name="arg5">Fifth constructor argument.</param>
            /// <param name="arg6">Sixth constructor argument.</param>
            /// <param name="arg7">Seventh constructor argument.</param>
            /// <param name="arg8">Eighth constructor argument.</param>
            /// <param name="arg9">Ninth constructor argument.</param>
            /// <param name="arg10">Tenth constructor argument.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
            /// <returns>Instance of <typeparamref name="T"/>.</returns>
            /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
            public static T Invoke(P1 arg1, P2 arg2, P3 arg3, P4 arg4, P5 arg5, P6 arg6, P7 arg7, P8 arg8, P9 arg9, P10 arg10, bool nonPublic = false)
                => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        }
    }
}