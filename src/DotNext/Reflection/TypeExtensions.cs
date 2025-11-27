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
    /// Extends <see cref="Type"/> type.
    /// </summary>
    /// <param name="type">The type to be discovered.</param>
    extension([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        /// <summary>
        /// Returns read-only collection of base types and, optionally, all implemented interfaces.
        /// </summary>
        /// <param name="includeTopLevel"><see langword="true"/> to return <paramref name="type"/> as first element in the collection.</param>
        /// <param name="includeInterfaces"><see langword="true"/> to include implemented interfaces; <see langword="false"/> to return inheritance hierarchy only.</param>
        /// <returns>Read-only collection of base types and, optionally, all implemented interfaces.</returns>
        public IEnumerable<Type> GetBaseTypes(bool includeTopLevel = false, bool includeInterfaces = false)
        {
            for (var lookup = includeTopLevel ? type : type.BaseType; lookup is not null; lookup = lookup.BaseType)
                yield return lookup;

            if (includeInterfaces)
            {
                foreach (var iface in type.GetInterfaces())
                    yield return iface;
            }
        }

        internal Type? FindGenericInstance(Type genericDefinition)
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
                    for (var lookup = type; lookup is not null; lookup = lookup.BaseType)
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
        /// <param name="genericDefinition">Generic type definition.</param>
        /// <returns><see langword="true"/>, if the type is an instance of the specified generic type; otherwise, <see langword="false"/>.</returns>
        /// <example>
        /// <code>
        /// typeof(byte[]).IsGenericInstanceOf(typeof(IEnumerable&lt;&gt;));    //returns true
        /// typeof(List&lt;int&gt;).IsGenericInstanceOf(typeof(List&lt;int&gt;));   //returns true
        /// </code>
        /// </example>
        public bool IsGenericInstanceOf(Type genericDefinition)
            => FindGenericInstance(type, genericDefinition) is not null;

        /// <summary>
        /// Returns actual generic arguments passed into generic type definition implemented by the input type.
        /// </summary>
        /// <param name="genericDefinition">The definition of generic type.</param>
        /// <returns>The array of actual generic types required by <paramref name="genericDefinition"/>.</returns>
        /// <example>
        /// <code>
        /// var elementTypes = typeof(byte[]).IsGenericInstanceOf(typeof(IEnumerable&lt;&gt;));
        /// elementTypes[0] == typeof(byte); //true
        /// </code>
        /// </example>
        public Type[] GetGenericArguments(Type genericDefinition)
            => FindGenericInstance(type, genericDefinition)?.GetGenericArguments() ?? [];
    }

    /// <summary>
    /// Extends <see cref="Type"/> type.
    /// </summary>
    /// <param name="type">The type to extend.</param>
    extension(Type type)
    {
        /// <summary>
        /// Determines whether the type is read-only (immutable) value type.
        /// </summary>
        /// <returns><see langword="true"/>, if the specified type is immutable value type; otherwise, <see langword="false"/>.</returns>
        public bool IsImmutable
            => type is { IsPrimitive: true } or { IsPointer: true } or { IsEnum: true } or { IsByRef: true } || type.IsValueType && type.IsDefined<IsReadOnlyAttribute>();
        
        /// <summary>
        /// Determines whether the type is unmanaged value type.
        /// </summary>
        /// <returns><see langword="true"/>, if the specified type is unmanaged value type; otherwise, <see langword="false"/>.</returns>
        public bool IsUnmanaged
        {
            [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
            get
            {
                return type switch
                {
                    { IsGenericTypeDefinition: true } => type.GetCustomAttributesData()
                        .Any(static attribute => attribute.AttributeType.FullName is IsUnmanagedAttributeName),
                    { IsPrimitive: true } or { IsPointer: true } or { IsEnum: true } => true,
                    { IsValueType: true } =>
                        // check all fields
                        type.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic)
                            .All(static f => f.FieldType.IsUnmanaged),
                    _ => false
                };
            }
        }

        /// <summary>
        /// Gets a value indicating that the type is <see langword="void"/>.
        /// </summary>
        public bool IsVoid => typeof(void) == type;
        
        /// <summary>
        /// Casts an object to the class, value type or interface.
        /// </summary>
        /// <param name="obj">The object to be cast.</param>
        /// <returns>The object after casting, or <see langword="null"/> if <paramref name="obj"/> is <see langword="null"/>.</returns>
        /// <exception cref="InvalidCastException">
        /// If the object is not <see langword="null"/> and is not assignable to the <paramref name="type"/>;
        /// or if object is <see langword="null"/> and <paramref name="type"/> is value type.
        /// </exception>
        [return: NotNullIfNotNull(nameof(obj))]
        public object? Cast(object? obj)
        {
            if (obj is null)
                return type.IsValueType ? throw new InvalidCastException(ExceptionMessages.CastNullToValueType) : null;
            
            return type.IsInstanceOfType(obj) ? obj : throw new InvalidCastException();
        }
        
        /// <summary>
        /// Indicates that object of one type can be implicitly converted into another without boxing.
        /// </summary>
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
        public bool IsAssignableFromWithoutBoxing(Type from)
            => type == from || !from.IsValueType && type.IsAssignableFrom(from);
        
        /// <summary>
        /// Returns method that overrides the specified method.
        /// </summary>
        /// <param name="abstractMethod">The abstract method definition.</param>
        /// <returns>The method that overrides <paramref name="abstractMethod"/>.</returns>
        [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
        public MethodInfo? Devirtualize(MethodInfo abstractMethod)
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
    }

    /// <summary>
    /// Extends <see cref="Type"/> type.
    /// </summary>
    /// <param name="type">The type to extend.</param>
    extension([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type type)
    {
        /// <summary>
        /// Gets default value for the specified type.
        /// </summary>
        /// <remarks>
        /// The method returns <see langword="null"/> for all reference and pointer types
        /// and default boxed value for value types.
        /// </remarks>
        /// <value>The default value of type <paramref name="type"/>.</value>
        public object? DefaultValue => type.IsValueType ? RuntimeHelpers.GetUninitializedObject(type) : null;
    }
}