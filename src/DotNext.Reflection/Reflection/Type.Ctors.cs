using DefaultMemberAttribute = System.Reflection.DefaultMemberAttribute;

namespace DotNext.Reflection;

public static partial class Type<T>
{
    /// <summary>
    /// Reflects constructor as function.
    /// </summary>
    /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
    /// <typeparam name="TArgs">A structure describing constructor signature.</typeparam>
    /// <returns>Constructor for type <typeparamref name="T"/>; or null, if it doesn't exist.</returns>
    public static Reflection.Constructor<Function<TArgs, T>>? GetConstructor<TArgs>(bool nonPublic = false)
        where TArgs : struct
        => Constructor.Get<Function<TArgs, T>>(nonPublic);

    /// <summary>
    /// Reflects constructor as function.
    /// </summary>
    /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
    /// <typeparam name="TArgs">A structure describing constructor signature.</typeparam>
    /// <returns>Constructor for type <typeparamref name="T"/>.</returns>
    /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
    public static Reflection.Constructor<Function<TArgs, T>> RequireConstructor<TArgs>(bool nonPublic = false)
        where TArgs : struct
        => GetConstructor<TArgs>(nonPublic) ?? throw MissingConstructorException.Create<T, TArgs>();

    /// <summary>
    /// Creates a new instance of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="args">The structure containing arguments to be passed into constructor.</param>
    /// <param name="nonPublic">True to reflect non-public constructor.</param>
    /// <typeparam name="TArgs">A structure describing constructor signature.</typeparam>
    /// <returns>A new instance of type <typeparamref name="T"/>.</returns>
    /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
    public static T NewInstance<TArgs>(in TArgs args, bool nonPublic = false)
        where TArgs : struct
         => RequireConstructor<TArgs>(nonPublic).Invoke(args)!;

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> without parameters.
    /// </summary>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor
    {
        /// <summary>
        /// Reflects constructor of type <typeparamref name="T"/> which signature
        /// is specified by delegate type.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <typeparam name="TSignature">Type of delegate describing constructor signature.</typeparam>
        /// <returns>Reflected constructor; or <see langword="null"/>, if constructor doesn't exist.</returns>
        public static Reflection.Constructor<TSignature>? Get<TSignature>(bool nonPublic = false)
            where TSignature : MulticastDelegate
            => Reflection.Constructor<TSignature>.GetOrCreate<T>(nonPublic);

        /// <summary>
        /// Reflects constructor of type <typeparamref name="T"/> which signature
        /// is specified by delegate type.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <typeparam name="TSignature">Type of delegate describing constructor signature.</typeparam>
        /// <returns>Reflected constructor.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<TSignature> Require<TSignature>(bool nonPublic = false)
            where TSignature : MulticastDelegate
            => Get<TSignature>(nonPublic) ?? throw MissingConstructorException.Create<TSignature>();

        /// <summary>
        /// Returns public constructor of type <typeparamref name="T"/> without parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor without parameters; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T>>? Get(bool nonPublic = false)
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
            Func<T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor();
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
    /// <typeparam name="TParam">Type of constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<TParam>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with single parameter of type <typeparamref name="TParam"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with single parameter; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<TParam, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<TParam, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with single parameter of type <typeparamref name="TParam"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with single parameter.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<TParam, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, TParam>>();

        /// <summary>
        /// Invokes constructor.
        /// </summary>
        /// <param name="arg">Constructor argument.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
        public static Optional<T> TryInvoke(TParam arg, bool nonPublic = false)
        {
            Func<TParam, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg);
        }

        /// <summary>
        /// Invokes constructor.
        /// </summary>
        /// <param name="arg">Constructor argument.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static T Invoke(TParam arg, bool nonPublic = false) => Require(nonPublic).Invoke(arg);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with two parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with two
        /// parameters of type <typeparamref name="T1"/> and <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with two parameters; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with two
        /// parameters of type <typeparamref name="T1"/> and <typeparamref name="T2"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with two parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2>>();

        /// <summary>
        /// Invokes constructor.
        /// </summary>
        /// <param name="arg1">First constructor argument.</param>
        /// <param name="arg2">Second constructor argument.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, bool nonPublic = false)
        {
            Func<T1, T2, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2);
        }

        /// <summary>
        /// Invokes constructor.
        /// </summary>
        /// <param name="arg1">First constructor argument.</param>
        /// <param name="arg2">Second constructor argument.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static T Invoke(T1 arg1, T2 arg2, bool nonPublic = false) => Require(nonPublic).Invoke(arg1, arg2);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with three parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    /// <typeparam name="T3">Type of third constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2, T3>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with three
        /// parameters of type <typeparamref name="T1"/>, <typeparamref name="T2"/> and <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with three parameters; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T3, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T3, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with three
        /// parameters of type <typeparamref name="T1"/>, <typeparamref name="T2"/> and <typeparamref name="T3"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with three parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T3, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2, T3>>();

        /// <summary>
        /// Invokes constructor.
        /// </summary>
        /// <param name="arg1">First constructor argument.</param>
        /// <param name="arg2">Second constructor argument.</param>
        /// <param name="arg3">Third constructor argument.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, T3 arg3, bool nonPublic = false)
        {
            Func<T1, T2, T3, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2, arg3);
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
        public static T Invoke(T1 arg1, T2 arg2, T3 arg3, bool nonPublic = false) => Require(nonPublic).Invoke(arg1, arg2, arg3);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with four parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    /// <typeparam name="T3">Type of third constructor parameter.</typeparam>
    /// <typeparam name="T4">Type of fourth constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2, T3, T4>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with four
        /// parameters of type <typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/> and <typeparamref name="T4"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with four parameters; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T3, T4, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with four
        /// parameters of type <typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/> and <typeparamref name="T4"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with four parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2, T3, T4>>();

        /// <summary>
        /// Invokes constructor.
        /// </summary>
        /// <param name="arg1">First constructor argument.</param>
        /// <param name="arg2">Second constructor argument.</param>
        /// <param name="arg3">Third constructor argument.</param>
        /// <param name="arg4">Fourth constructor argument.</param>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Instance of <typeparamref name="T"/> if constructor exists.</returns>
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, bool nonPublic = false)
        {
            Func<T1, T2, T3, T4, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2, arg3, arg4);
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
        public static T Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, bool nonPublic = false) => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with five parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    /// <typeparam name="T3">Type of third constructor parameter.</typeparam>
    /// <typeparam name="T4">Type of fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">Type of fifth constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2, T3, T4, T5>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with five
        /// parameters of type <typeparamref name="T1"/>, <typeparamref name="T2"/>,
        /// <typeparamref name="T3"/>, <typeparamref name="T4"/> and <typeparamref name="T5"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with five parameters; or null, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T3, T4, T5, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with five
        /// parameters of type <typeparamref name="T1"/>, <typeparamref name="T2"/>,
        /// <typeparamref name="T3"/>, <typeparamref name="T4"/> and <typeparamref name="T5"/>.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with five parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2, T3, T4, T5>>();

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
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, bool nonPublic = false)
        {
            Func<T1, T2, T3, T4, T5, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2, arg3, arg4, arg5);
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
        public static T Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, bool nonPublic = false)
            => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with six parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    /// <typeparam name="T3">Type of third constructor parameter.</typeparam>
    /// <typeparam name="T4">Type of fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">Type of fifth constructor parameter.</typeparam>
    /// <typeparam name="T6">Type of sixth constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2, T3, T4, T5, T6>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with six parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with six parameters; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T3, T4, T5, T6, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with six parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with six parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2, T3, T4, T5, T6>>();

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
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, bool nonPublic = false)
        {
            Func<T1, T2, T3, T4, T5, T6, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2, arg3, arg4, arg5, arg6);
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
        public static T Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, bool nonPublic = false)
            => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with seven parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    /// <typeparam name="T3">Type of third constructor parameter.</typeparam>
    /// <typeparam name="T4">Type of fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">Type of fifth constructor parameter.</typeparam>
    /// <typeparam name="T6">Type of sixth constructor parameter.</typeparam>
    /// <typeparam name="T7">Type of seventh constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2, T3, T4, T5, T6, T7>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with seven parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with seven parameters; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T7, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T3, T4, T5, T6, T7, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with seven parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with seven parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T7, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2, T3, T4, T5, T6, T7>>();

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
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, bool nonPublic = false)
        {
            Func<T1, T2, T3, T4, T5, T6, T7, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
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
        public static T Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, bool nonPublic = false)
            => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with eight parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    /// <typeparam name="T3">Type of third constructor parameter.</typeparam>
    /// <typeparam name="T4">Type of fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">Type of fifth constructor parameter.</typeparam>
    /// <typeparam name="T6">Type of sixth constructor parameter.</typeparam>
    /// <typeparam name="T7">Type of seventh constructor parameter.</typeparam>
    /// <typeparam name="T8">Type of eighth constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2, T3, T4, T5, T6, T7, T8>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with eight parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with eight parameters; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T7, T8, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T3, T4, T5, T6, T7, T8, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with eight parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with eight parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T7, T8, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2, T3, T4, T5, T6, T7, T8>>();

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
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, bool nonPublic = false)
        {
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
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
        public static T Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, bool nonPublic = false)
            => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with nine parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    /// <typeparam name="T3">Type of third constructor parameter.</typeparam>
    /// <typeparam name="T4">Type of fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">Type of fifth constructor parameter.</typeparam>
    /// <typeparam name="T6">Type of sixth constructor parameter.</typeparam>
    /// <typeparam name="T7">Type of seventh constructor parameter.</typeparam>
    /// <typeparam name="T8">Type of eighth constructor parameter.</typeparam>
    /// <typeparam name="T9">Type of ninth constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2, T3, T4, T5, T6, T7, T8, T9>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with nine parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with nine parameters; or <see langword="true"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with nine parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with nine parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2, T3, T4, T5, T6, T7, T8, T9>>();

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
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, bool nonPublic = false)
        {
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
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
        public static T Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, bool nonPublic = false)
            => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    }

    /// <summary>
    /// Provides access to constructor of type <typeparamref name="T"/> with nine parameters.
    /// </summary>
    /// <typeparam name="T1">Type of first constructor parameter.</typeparam>
    /// <typeparam name="T2">Type of second constructor parameter.</typeparam>
    /// <typeparam name="T3">Type of third constructor parameter.</typeparam>
    /// <typeparam name="T4">Type of fourth constructor parameter.</typeparam>
    /// <typeparam name="T5">Type of fifth constructor parameter.</typeparam>
    /// <typeparam name="T6">Type of sixth constructor parameter.</typeparam>
    /// <typeparam name="T7">Type of seventh constructor parameter.</typeparam>
    /// <typeparam name="T8">Type of eighth constructor parameter.</typeparam>
    /// <typeparam name="T9">Type of ninth constructor parameter.</typeparam>
    /// <typeparam name="T10">Type of tenth constructor parameter.</typeparam>
    [DefaultMember(nameof(Invoke))]
    public static class Constructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>
    {
        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with ten parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with ten parameters; or <see langword="null"/>, if it doesn't exist.</returns>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T>>? Get(bool nonPublic = false)
            => Constructor.Get<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T>>(nonPublic);

        /// <summary>
        /// Returns constructor <typeparamref name="T"/> with ten parameters.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> to reflect non-public constructor.</param>
        /// <returns>Reflected constructor with ten parameters.</returns>
        /// <exception cref="MissingConstructorException">Constructor doesn't exist.</exception>
        public static Reflection.Constructor<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T>> Require(bool nonPublic = false)
            => Get(nonPublic) ?? throw MissingConstructorException.Create<Func<T, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();

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
        public static Optional<T> TryInvoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, bool nonPublic = false)
        {
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T>? ctor = Get(nonPublic);
            return ctor is null ? Optional<T>.None : ctor(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
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
        public static T Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, bool nonPublic = false)
            => Require(nonPublic).Invoke(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }
}