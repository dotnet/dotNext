using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection;

/// <summary>
/// Various extension methods for type reflection.
/// </summary>
public static class TypeExtensions
{
    private const string IsUnmanagedAttributeName = "System.Runtime.CompilerServices.IsUnmanagedAttribute";

    /// <summary>
    /// Determines whether the type is read-only (immutable) value type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/>, if the specified type is immutable value type; otherwise, <see langword="false"/>.</returns>
    public static bool IsImmutable(this Type type)
        => (type is { IsPrimitive: true } or { IsPointer: true } or { IsEnum: true } or { IsByRef: true }) || type.IsValueType && type.IsDefined<IsReadOnlyAttribute>();

    /// <summary>
    /// Determines whether the type is unmanaged value type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/>, if the specified type is unmanaged value type; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    public static bool IsUnmanaged(this Type type)
    {
        switch (type)
        {
            case { IsGenericTypeDefinition: true }:
                foreach (var attribute in type.GetCustomAttributesData())
                {
                    if (attribute.AttributeType.FullName == IsUnmanagedAttributeName)
                        return true;
                }

                break;
            case { IsPrimitive: true } or { IsPointer: true } or { IsEnum: true }:
                return true;

            case { IsValueType: true }:
                // check all fields
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!field.FieldType.IsUnmanaged())
                        return false;
                }

                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns read-only collection of base types and, optionally, all implemented interfaces.
    /// </summary>
    /// <param name="type">The type to be discovered.</param>
    /// <param name="includeTopLevel"><see langword="true"/> to return <paramref name="type"/> as first element in the collection.</param>
    /// <param name="includeInterfaces"><see langword="true"/> to include implemented interfaces; <see langword="false"/> to return inheritance hierarchy only.</param>
    /// <returns>Read-only collection of base types and, optionally, all implemented interfaces.</returns>
    public static IEnumerable<Type> GetBaseTypes([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, bool includeTopLevel = false, bool includeInterfaces = false)
    {
        for (var lookup = includeTopLevel ? type : type.BaseType; lookup is not null; lookup = lookup.BaseType)
            yield return lookup;

        if (includeInterfaces)
        {
            foreach (var iface in type.GetInterfaces())
                yield return iface;
        }
    }

    /// <summary>
    /// Returns method that overrides the specified method.
    /// </summary>
    /// <param name="type">The type that contains overridden method.</param>
    /// <param name="abstractMethod">The abstract method definition.</param>
    /// <returns>The method that overrides <paramref name="abstractMethod"/>.</returns>
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    public static MethodInfo? Devirtualize(this Type type, MethodInfo abstractMethod)
    {
        if (abstractMethod is { IsFinal: true } or { IsVirtual: false } or { DeclaringType: null })
            return abstractMethod;

        if (type.IsInterface)
            goto exit;

        if (abstractMethod.DeclaringType.IsInterface && abstractMethod.DeclaringType.IsAssignableFrom(type))
        {
            // Interface maps for generic interfaces on arrays cannot be retrieved.
            if (type.IsArray && abstractMethod.DeclaringType.IsGenericType)
                goto exit;

            var interfaceMap = type.GetInterfaceMap(abstractMethod.DeclaringType);
            for (var i = 0L; i < interfaceMap.InterfaceMethods.LongLength; i++)
            {
                if (interfaceMap.InterfaceMethods[i] == abstractMethod)
                    return interfaceMap.TargetMethods[i];
            }

            goto exit;
        }

        // handle virtual method
        foreach (var lookup in GetBaseTypes(type, includeTopLevel: true))
        {
            foreach (var candidate in lookup.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (candidate.GetBaseDefinition() == abstractMethod)
                    return candidate;
            }
        }

    exit:
        return null;
    }

    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
    internal static Type? FindGenericInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, Type genericDefinition)
    {
        bool IsGenericInstanceOf(Type candidate)
            => candidate.IsConstructedGenericType && candidate.GetGenericTypeDefinition() == genericDefinition;

        switch (type)
        {
            case { IsGenericTypeDefinition: true } when genericDefinition.IsGenericTypeDefinition is false:
                return null;
            case { IsConstructedGenericType: true } when type.GetGenericTypeDefinition() == genericDefinition:
                return type;
        }

        switch (genericDefinition)
        {
            case { IsSealed: true }:
                return IsGenericInstanceOf(type) ? type : null;
            case { IsInterface: true }:
                foreach (var iface in type.GetInterfaces())
                {
                    if (IsGenericInstanceOf(iface))
                        return iface;
                }

                break;
            default:
                for (Type? lookup = type; lookup is not null; lookup = lookup.BaseType)
                {
                    if (IsGenericInstanceOf(lookup))
                        return lookup;
                }

                break;
        }

        return null;
    }

    /// <summary>
    /// Determines whether the type is an instance of the specified generic type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="genericDefinition">Generic type definition.</param>
    /// <returns><see langword="true"/>, if the type is an instance of the specified generic type; otherwise, <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// typeof(byte[]).IsGenericInstanceOf(typeof(IEnumerable&lt;&gt;));    //returns true
    /// typeof(List&lt;int&gt;).IsGenericInstanceOf(typeof(List&lt;int&gt;));   //returns true
    /// </code>
    /// </example>
    public static bool IsGenericInstanceOf([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, Type genericDefinition)
        => FindGenericInstance(type, genericDefinition) is not null;

    /// <summary>
    /// Returns actual generic arguments passed into generic type definition implemented by the input type.
    /// </summary>
    /// <param name="type">The type that inherits from generic class or implements generic interface.</param>
    /// <param name="genericDefinition">The definition of generic type.</param>
    /// <returns>The array of actual generic types required by <paramref name="genericDefinition"/>.</returns>
    /// <example>
    /// <code>
    /// var elementTypes = typeof(byte[]).IsGenericInstanceOf(typeof(IEnumerable&lt;&gt;));
    /// elementTypes[0] == typeof(byte); //true
    /// </code>
    /// </example>
    public static Type[] GetGenericArguments([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, Type genericDefinition)
        => FindGenericInstance(type, genericDefinition)?.GetGenericArguments() ?? Type.EmptyTypes;

    /// <summary>
    /// Indicates that object of one type can be implicitly converted into another without boxing.
    /// </summary>
    /// <param name="to">Type of conversion result.</param>
    /// <param name="from">The type check.</param>
    /// <returns><see langword="true"/> if <paramref name="from"/> is implicitly convertible into <paramref name="to"/> without boxing.</returns>
    /// <seealso cref="Type.IsAssignableFrom(Type)"/>
    /// <example>
    /// <code>
    /// typeof(object).IsAssignableFrom(typeof(int)); //true
    /// typeof(object).IsAssignableFromWithoutBoxing(typeof(int)); //false
    /// typeof(object).IsAssignableFrom(typeof(string));    //true
    /// typeof(object).IsAssignableFromWithoutBoxing(typeof(string));//true
    /// </code>
    /// </example>
    public static bool IsAssignableFromWithoutBoxing(this Type to, Type from)
        => to == from || !from.IsValueType && to.IsAssignableFrom(from);

    /// <summary>
    /// Casts an object to the class, value type or interface.
    /// </summary>
    /// <param name="type">The type cast result.</param>
    /// <param name="obj">The object to be cast.</param>
    /// <returns>The object after casting, or <see langword="null"/> if <paramref name="obj"/> is <see langword="null"/>.</returns>
    /// <exception cref="InvalidCastException">
    /// If the object is not <see langword="null"/> and is not assignable to the <paramref name="type"/>;
    /// or if object is <see langword="null"/> and <paramref name="type"/> is value type.
    /// </exception>
    [return: NotNullIfNotNull(nameof(obj))]
    public static object? Cast(this Type type, object? obj)
    {
        if (obj is null)
            return type.IsValueType ? throw new InvalidCastException(ExceptionMessages.CastNullToValueType) : default(object);
        if (type.IsInstanceOfType(obj))
            return obj;
        throw new InvalidCastException();
    }

    /// <summary>
    /// Gets default value for the specified type.
    /// </summary>
    /// <remarks>
    /// The method returns <see langword="null"/> for all reference and pointer types
    /// and default boxed value for value types.
    /// </remarks>
    /// <param name="type">The type for which default value should be returned.</param>
    /// <returns>The default value of type <paramref name="type"/>.</returns>
    public static object? GetDefaultValue([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] this Type type)
        => type.IsValueType ? RuntimeHelpers.GetUninitializedObject(type) : null;
}