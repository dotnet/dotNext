using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection;

using Seq = Collections.Generic.Sequence;

/// <summary>
/// Provides constructor definition based on delegate signature.
/// </summary>
/// <typeparam name="TSignature">Type of delegate representing constructor of type <typeparamref name="TSignature"/>.</typeparam>
public sealed class Constructor<TSignature> : ConstructorInfo, IConstructor<TSignature>, IEquatable<ConstructorInfo?>
    where TSignature : MulticastDelegate
{
    private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public;
    private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.NonPublic;

    [SuppressMessage("Performance", "CA1805", Justification = "https://github.com/dotnet/roslyn-analyzers/issues/5750")]
    private static readonly UserDataSlot<Constructor<TSignature>?> CacheSlot = new();

    private readonly TSignature invoker;
    private readonly object ctorInfo;

    private Constructor(ConstructorInfo ctor, Expression<TSignature> invoker)
    {
        if (ctor.IsStatic || ctor.DeclaringType is null)
            throw new ArgumentException(ExceptionMessages.StaticCtorDetected, nameof(ctor));
        ctorInfo = ctor;
        this.invoker = invoker.Compile();
    }

    private Constructor(ConstructorInfo ctor, IEnumerable<Expression> args, IEnumerable<ParameterExpression> parameters)
        : this(ctor, Expression.Lambda<TSignature>(Expression.New(ctor, args), parameters))
    {
    }

    private Constructor(ConstructorInfo ctor, IEnumerable<ParameterExpression> parameters)
        : this(ctor, parameters, parameters)
    {
    }

    private Constructor(Type valueType, IEnumerable<ParameterExpression>? parameters = null)
    {
        ctorInfo = valueType;
        invoker = Expression.Lambda<TSignature>(Expression.Default(valueType), parameters ?? Enumerable.Empty<ParameterExpression>()).Compile();
    }

    /// <summary>
    /// Extracts delegate which can be used to invoke this constructor.
    /// </summary>
    /// <param name="ctor">The reflected constructor.</param>
    [return: NotNullIfNotNull("ctor")]
    public static implicit operator TSignature?(Constructor<TSignature>? ctor) => ctor?.invoker;

    /// <summary>
    /// Gets name of the constructor.
    /// </summary>
    public override string Name => ConstructorName;

    /// <inheritdoc/>
    ConstructorInfo IMember<ConstructorInfo>.Metadata => ctorInfo as ConstructorInfo ?? this;

    /// <inheritdoc/>
    TSignature IMember<ConstructorInfo, TSignature>.Invoker => invoker;

    /// <summary>
    /// Gets the attributes associated with this constructor.
    /// </summary>
    public override MethodAttributes Attributes => (ctorInfo as ConstructorInfo)?.Attributes ?? MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

    /// <summary>
    /// Gets a handle to the internal metadata representation of a constructor.
    /// </summary>
    public override RuntimeMethodHandle MethodHandle => (ctorInfo as MethodBase ?? invoker.Method).MethodHandle;

    /// <summary>
    /// Gets the class that declares this constructor.
    /// </summary>
    public override Type DeclaringType
    {
        get
        {
            switch (ctorInfo)
            {
                case ConstructorInfo ctor:
                    Debug.Assert(ctor.DeclaringType is not null);
                    return ctor.DeclaringType;
                case Type vt:
                    Debug.Assert(vt.IsValueType);
                    return vt;
                default:
                    return GetType();
            }
        }
    }

    /// <summary>
    /// Gets the class object that was used to obtain this instance.
    /// </summary>
    public override Type? ReflectedType => (ctorInfo as MethodBase ?? invoker.Method).ReflectedType;

    /// <summary>
    /// Gets a value indicating the calling conventions for this constructor.
    /// </summary>
    public override CallingConventions CallingConvention => (ctorInfo as MethodBase ?? invoker.Method).CallingConvention;

    /// <summary>
    /// Gets a value indicating whether the generic method contains unassigned generic type parameters.
    /// </summary>
    public override bool ContainsGenericParameters => false;

    /// <summary>
    /// Gets a collection that contains this member's custom attributes.
    /// </summary>
    public override IEnumerable<CustomAttributeData> CustomAttributes => GetCustomAttributesData();

    /// <summary>
    /// Provides access to the MSIL stream, local variables, and exceptions for the current constructor.
    /// </summary>
    /// <returns>An object that provides access to the MSIL stream, local variables, and exceptions for the current constructor.</returns>
    public override MethodBody? GetMethodBody() => (ctorInfo as MethodBase ?? invoker.Method).GetMethodBody();

    /// <summary>
    /// Returns a list of custom attributes that have been applied to the target constructor.
    /// </summary>
    /// <returns>The data about the attributes that have been applied to the target constructor.</returns>
    public override IList<CustomAttributeData> GetCustomAttributesData() => (ctorInfo as MethodBase ?? invoker.Method).GetCustomAttributesData();

    /// <summary>
    /// Returns the type arguments of a generic method or the type parameters of a generic method definition.
    /// </summary>
    /// <returns>The list of generic arguments.</returns>
    public override Type[] GetGenericArguments() => Array.Empty<Type>();

    /// <summary>
    /// Gets a value indicating whether the constructor is generic.
    /// </summary>
    public override bool IsGenericMethod => false;

    /// <summary>
    /// Gets a value indicating whether the constructor is a generic method definition.
    /// </summary>
    public override bool IsGenericMethodDefinition => false;

    /// <summary>
    /// Gets a value that indicates whether the constructor is security-critical or security-safe-critical at the current trust level,
    /// and therefore can perform critical operations.
    /// </summary>
    public override bool IsSecurityCritical => ctorInfo switch
    {
        MethodBase ctor => ctor.IsSecurityCritical,
        Type vt => vt.IsSecurityCritical,
        _ => invoker.Method.IsSecurityCritical
    };

    /// <summary>
    /// Gets a value that indicates whether the constructor is security-safe-critical at the current trust level; that is,
    /// whether it can perform critical operations and can be accessed by transparent code.
    /// </summary>
    public override bool IsSecuritySafeCritical => ctorInfo switch
    {
        MethodBase ctor => ctor.IsSecuritySafeCritical,
        Type vt => vt.IsSecuritySafeCritical,
        _ => invoker.Method.IsSecuritySafeCritical
    };

    /// <summary>
    /// Gets a value that indicates whether the current constructor is transparent at the current trust level,
    /// and therefore cannot perform critical operations.
    /// </summary>
    public override bool IsSecurityTransparent => ctorInfo switch
    {
        MethodBase ctor => ctor.IsSecurityTransparent,
        Type vt => vt.IsSecurityTransparent,
        _ => invoker.Method.IsSecurityTransparent
    };

    /// <summary>
    /// Always returns <see cref="MemberTypes.Constructor"/>.
    /// </summary>
    public override MemberTypes MemberType => MemberTypes.Constructor;

    /// <summary>
    /// Gets a value that identifies a metadata element.
    /// </summary>
    public override int MetadataToken => (ctorInfo as MethodBase ?? invoker.Method).MetadataToken;

    /// <summary>
    /// Gets constructor implementation attributes.
    /// </summary>
    public override MethodImplAttributes MethodImplementationFlags => (ctorInfo as MethodBase ?? invoker.Method).MethodImplementationFlags;

    /// <summary>
    /// Gets the module in which the type that declares the constructor represented by the current instance is defined.
    /// </summary>
    public override Module Module => DeclaringType.Module;

    /// <summary>
    /// Invokes this constructor.
    /// </summary>
    /// <param name="invokeAttr">Specifies the type of binding.</param>
    /// <param name="binder">Defines a set of properties and enables the binding, coercion of argument types, and invocation of members using reflection.</param>
    /// <param name="parameters">A list of constructor arguments.</param>
    /// <param name="culture">Used to govern the coercion of types.</param>
    /// <returns>Instantiated object.</returns>
    public override object Invoke(BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
        => Invoke(null, invokeAttr, binder, parameters, culture);

    /// <summary>
    /// Gets constructor implementation attributes.
    /// </summary>
    /// <returns>Implementation attributes.</returns>
    public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplementationFlags;

    /// <summary>
    /// Gets constructor parameters.
    /// </summary>
    /// <returns>The array of constructor parameters.</returns>
    public override ParameterInfo[] GetParameters() => (ctorInfo as MethodBase ?? invoker.Method).GetParameters();

    /// <summary>
    /// Invokes this constructor.
    /// </summary>
    /// <param name="obj">The object on which to invoke the constructor.</param>
    /// <param name="invokeAttr">Specifies the type of binding.</param>
    /// <param name="binder">Defines a set of properties and enables the binding, coercion of argument types, and invocation of members using reflection.</param>
    /// <param name="parameters">A list of constructor arguments.</param>
    /// <param name="culture">Used to govern the coercion of types.</param>
    /// <returns>Instantiated object.</returns>
    public override object Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
        => (ctorInfo as MethodBase ?? invoker.Method).Invoke(obj, invokeAttr, binder, parameters, culture)!;

    /// <summary>
    /// Returns an array of all custom attributes applied to this constructor.
    /// </summary>
    /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
    /// <returns>An array that contains all the custom attributes applied to this constructor.</returns>
    public override object[] GetCustomAttributes(bool inherit)
        => (ctorInfo as MethodBase ?? invoker.Method).GetCustomAttributes(inherit);

    /// <summary>
    /// Returns an array of all custom attributes applied to this constructor.
    /// </summary>
    /// <param name="attributeType">The type of attribute to search for. Only attributes that are assignable to this type are returned.</param>
    /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
    /// <returns>An array that contains all the custom attributes applied to this constructor.</returns>
    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        => (ctorInfo as MethodBase ?? invoker.Method).GetCustomAttributes(attributeType, inherit);

    /// <summary>
    /// Determines whether one or more attributes of the specified type or of its derived types is applied to this constructor.
    /// </summary>
    /// <param name="attributeType">The type of custom attribute to search for. The search includes derived types.</param>
    /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if one or more instances of <paramref name="attributeType"/> or any of its derived types is applied to this constructor; otherwise, <see langword="false"/>.</returns>
    public override bool IsDefined(Type attributeType, bool inherit)
        => (ctorInfo as MethodBase ?? invoker.Method).IsDefined(attributeType, inherit);

    /// <summary>
    /// Determines whether this constructor is equal to the given constructor.
    /// </summary>
    /// <param name="other">Other constructor to compare.</param>
    /// <returns><see langword="true"/> if this object reflects the same constructor as the specified object; otherwise, <see langword="false"/>.</returns>
    public bool Equals(ConstructorInfo? other) => other is Constructor<TSignature> ctor ? Equals(ctorInfo, ctor.ctorInfo) : Equals(ctorInfo, other);

    /// <summary>
    /// Determines whether this constructor is equal to the given constructor.
    /// </summary>
    /// <param name="other">Other constructor to compare.</param>
    /// <returns><see langword="true"/> if this object reflects the same constructor as the specified object; otherwise, <see langword="false"/>.</returns>
    public override bool Equals([NotNullWhen(true)] object? other) => other switch
    {
        Constructor<TSignature> ctor => Equals(ctorInfo, ctor.ctorInfo),
        ConstructorInfo ctor => Equals(ctorInfo, ctor),
        TSignature invoker => Equals(this.invoker, invoker),
        _ => false,
    };

    /// <summary>
    /// Returns textual representation of this constructor.
    /// </summary>
    /// <returns>The textual representation of this constructor.</returns>
    public override string? ToString() => (ctorInfo as MethodBase ?? invoker.Method).ToString();

    /// <summary>
    /// Computes hash code uniquely identifies the reflected constructor.
    /// </summary>
    /// <returns>The hash code of the constructor.</returns>
    public override int GetHashCode() => ctorInfo.GetHashCode();

    private static Constructor<TSignature>? Reflect(Type declaringType, Type[] parameters, bool nonPublic)
    {
        ConstructorInfo? ctor = declaringType.GetConstructor(nonPublic ? NonPublicFlags : PublicFlags, Type.DefaultBinder, parameters, Array.Empty<ParameterModifier>());
        if (ctor is null)
            return declaringType.IsValueType && parameters.Length is 0 ? new(declaringType) : null;
        return new(ctor, Array.ConvertAll(parameters, Expression.Parameter));
    }

    private static Constructor<TSignature>? Reflect(Type declaringType, Type argumentsType, bool nonPublic)
    {
        var (parameters, arglist, input) = Signature.Reflect(argumentsType);
        ConstructorInfo? ctor = declaringType.GetConstructor(nonPublic ? NonPublicFlags : PublicFlags, Type.DefaultBinder, parameters, Array.Empty<ParameterModifier>());

        if (ctor is null)
            return declaringType.IsValueType && parameters.Length is 0 ? new(declaringType, Seq.Singleton(input)) : null;
        return new(ctor, arglist, Seq.Singleton(input));
    }

    private static Constructor<TSignature>? Reflect(bool nonPublic)
    {
        var delegateType = typeof(TSignature);
        if (delegateType.IsGenericInstanceOf(typeof(Function<,>)) && typeof(TSignature).GetGenericArguments().Take(out var argumentsType, out var declaringType))
        {
            return Reflect(declaringType, argumentsType, nonPublic);
        }
        else if (delegateType.IsAbstract)
        {
            throw new AbstractDelegateException<TSignature>();
        }
        else
        {
            var invokeMethod = DelegateType.GetInvokeMethod<TSignature>();
            return Reflect(invokeMethod.ReturnType, invokeMethod.GetParameterTypes(), nonPublic);
        }
    }

    private static Constructor<TSignature>? Unreflect(ConstructorInfo ctor, Type argumentsType, Type returnType)
    {
        Debug.Assert(ctor.DeclaringType is not null);
        var (_, arglist, input) = Signature.Reflect(argumentsType);
        var prologue = new LinkedList<Expression>();
        var epilogue = new LinkedList<Expression>();
        var locals = new LinkedList<ParameterExpression>();

        // adjust arguments
        if (!Signature.NormalizeArguments(ctor.GetParameters(), arglist, locals, prologue, epilogue))
            return null;
        Expression body;

        // adjust return type
        if (returnType == typeof(void) || returnType.IsAssignableFromWithoutBoxing(ctor.DeclaringType))
            body = Expression.New(ctor, arglist);
        else if (returnType == typeof(object))
            body = Expression.Convert(Expression.New(ctor, arglist), returnType);
        else
            return null;
        if (epilogue.Count == 0)
        {
            epilogue.AddFirst(body);
        }
        else
        {
            var returnArg = Expression.Parameter(returnType);
            locals.AddFirst(returnArg);
            body = Expression.Assign(returnArg, body);
            epilogue.AddFirst(body);
            epilogue.AddLast(returnArg);
        }

        body = prologue.Count is 0 && epilogue.Count is 1 ? epilogue.First!.Value : Expression.Block(locals, prologue.Concat(epilogue));
        return new(ctor, Expression.Lambda<TSignature>(body, input));
    }

    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
    private static Constructor<TSignature>? Unreflect(ConstructorInfo ctor)
    {
        var delegateType = typeof(TSignature);
        if (delegateType.IsAbstract)
            throw new AbstractDelegateException<TSignature>();

        switch (ctor)
        {
            case Constructor<TSignature> existing:
                return existing;
            case { IsGenericMethodDefinition: true } or { IsAbstract: true }:
                return null;
        }

        if (delegateType.IsGenericInstanceOf(typeof(Function<,>)) && delegateType.GetGenericArguments().Take(out var argumentsType, out var returnType))
            return Unreflect(ctor, argumentsType, returnType);

        var invokeMethod = DelegateType.GetInvokeMethod<TSignature>();
        return ctor.SignatureEquals(invokeMethod) && invokeMethod.ReturnType.IsAssignableFrom(ctor.DeclaringType) ?
            new(ctor, Array.ConvertAll(ctor.GetParameterTypes(), Expression.Parameter)) :
            null;
    }

    internal static unsafe Constructor<TSignature>? GetOrCreate(ConstructorInfo ctor)
        => ctor.GetUserData().GetOrSet(CacheSlot, ctor, &Unreflect);

    internal static unsafe Constructor<TSignature>? GetOrCreate<T>(bool nonPublic)
    {
        var type = typeof(T);
        var ctor = type.GetUserData().GetOrSet(CacheSlot, nonPublic, &Reflect);
        return ctor?.DeclaringType == type ? ctor : null;
    }
}