using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Reflection;

/// <summary>
/// Provides specialized reflection methods for
/// collection types.
/// </summary>
public static class CollectionType
{
    internal const string ItemIndexerName = "Item";

    /// <summary>
    /// Obtains type of items in the collection type.
    /// </summary>
    /// <param name="collectionType">Any collection type implementing <see cref="IEnumerable{T}"/>.</param>
    /// <param name="enumerableInterface">The type <see cref="IEnumerable{T}"/> with actual generic argument.</param>
    /// <returns>Type of items in the collection; or <see langword="null"/> if <paramref name="collectionType"/> is not a generic collection.</returns>
    public static Type? GetItemType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicMethods)] this Type collectionType, out Type? enumerableInterface)
    {
        enumerableInterface = collectionType.FindGenericInstance(typeof(IEnumerable<>));
        if (enumerableInterface is not null)
            return enumerableInterface.GetGenericArguments()[0];

        if (typeof(IEnumerable).IsAssignableFrom(collectionType))
        {
            enumerableInterface = typeof(IEnumerable);
            return typeof(object);
        }

        // handle async enumerable type
        enumerableInterface = collectionType.FindGenericInstance(typeof(IAsyncEnumerable<>));
        if (enumerableInterface is not null)
            return enumerableInterface.GetGenericArguments()[0];

        // determine via GetEnumerator public method
        return collectionType.GetMethod(nameof(IEnumerable.GetEnumerator), BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance, []) is { ReturnType: { } returnType }
            && returnType.GetProperty(nameof(IEnumerator.Current), BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance) is { PropertyType: { } elementType }
            ? elementType
            : null;
    }
    
    /// <summary>
    /// Extends <see cref="System.Type"/> type.
    /// </summary>
    /// <param name="collectionType">Any collection type implementing <see cref="IEnumerable{T}"/>.</param>
    extension(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicMethods)] Type collectionType)
    {
        /// <summary>
        /// Obtains type of items in the collection type.
        /// </summary>
        /// <value>Type of items in the collection; or <see langword="null"/> if <paramref name="collectionType"/> is not a generic collection.</value>
        public Type? ItemType => collectionType.GetItemType(out _);
    }

    /// <summary>
    /// Extends <see cref="System.Type"/> type.
    /// </summary>
    /// <param name="type">The type that implements the one of the supported collection types.</param>
    extension([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        /// <summary>
        /// Returns type of collection implemented by the given type.
        /// </summary>
        /// <remarks>
        /// The supported collection types are <see cref="ICollection{T}"/>, <seealso cref="IReadOnlyCollection{T}"/>.
        /// </remarks>
        /// <value>The interface of the collection implemented by the given type; otherwise, <see langword="null"/> if collection interface is not implemented.</value>
        /// <seealso cref="ICollection{T}"/>
        /// <seealso cref="IReadOnlyCollection{T}"/>
        public Type? ImplementedCollection
        {
            get
            {
                ReadOnlySpan<Type> collectionTypes = [typeof(IReadOnlyCollection<>), typeof(ICollection<>)];
                foreach (var collectionType in collectionTypes)
                {
                    if (type.FindGenericInstance(collectionType) is { } result)
                        return result;
                }

                return null;
            }
        }
    }

    /// <summary>
    /// Extends <see cref="IReadOnlyList{T}"/> type.
    /// </summary>
    /// <typeparam name="T">Type of list items.</typeparam>
    extension<T>(IReadOnlyList<T>)
    {
        /// <summary>
        /// Represents read-only list item getter.
        /// </summary>
        public static Func<IReadOnlyList<T>, int, T> Indexer => Indexer<T>.ReadOnly;
    }

    /// <summary>
    /// Extends <see cref="IList{T}"/> type.
    /// </summary>
    /// <typeparam name="T">Type of list items.</typeparam>
    extension<T>(IList<T>)
    {
        /// <summary>
        /// Represents list item getter.
        /// </summary>
        public static Func<IList<T>, int, T> IndexerGetter => Indexer<T>.Getter;

        /// <summary>
        /// Represents list item setter.
        /// </summary>
        public static Action<IList<T>, int, T> IndexerSetter => Indexer<T>.Setter;
    }
    
    private static class Indexer<T>
    {
        public static readonly Func<IReadOnlyList<T>, int, T> ReadOnly;

        public static readonly Func<IList<T>, int, T> Getter;

        public static readonly Action<IList<T>, int, T> Setter;

        static Indexer()
        {
            Ldtoken(PropertyGet(Type<IReadOnlyList<T>>(), ItemIndexerName));
            Pop(out RuntimeMethodHandle method);
            Ldtoken(Type<IReadOnlyList<T>>());
            Pop(out RuntimeTypeHandle type);
            ReadOnly = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Func<IReadOnlyList<T>, int, T>>();

            Ldtoken(PropertyGet(Type<IList<T>>(), ItemIndexerName));
            Pop(out method);
            Ldtoken(Type<IList<T>>());
            Pop(out type);
            Getter = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Func<IList<T>, int, T>>();

            Ldtoken(PropertySet(Type<IList<T>>(), ItemIndexerName));
            Pop(out method);
            Setter = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Action<IList<T>, int, T>>();
        }
    }
}