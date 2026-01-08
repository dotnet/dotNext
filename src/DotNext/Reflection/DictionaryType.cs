using System.Reflection;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Reflection;

/// <summary>
/// Provides specialized reflection methods for
/// dictionary types.
/// </summary>
public static class DictionaryType
{
    /// <summary>
    /// Extends <see cref="IReadOnlyDictionary{TKey,TValue}"/> type.
    /// </summary>
    /// <typeparam name="TKey">Type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">Type of values in the dictionary.</typeparam>
    extension<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>)
    {
        /// <summary>
        /// Represents read-only dictionary indexer.
        /// </summary>
        public static Func<IReadOnlyDictionary<TKey, TValue>, TKey, TValue> Indexer => Indexer<TKey, TValue>.ReadOnly;
    }

    /// <summary>
    /// Extends <see cref="IDictionary{TKey,TValue}"/> type.
    /// </summary>
    /// <typeparam name="TKey">Type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">Type of values in the dictionary.</typeparam>
    extension<TKey, TValue>(IDictionary<TKey, TValue>)
    {
        /// <summary>
        /// Represents dictionary value getter.
        /// </summary>
        public static Func<IDictionary<TKey, TValue>, TKey, TValue> IndexerGetter => Indexer<TKey, TValue>.Getter;

        /// <summary>
        /// Represents dictionary value setter.
        /// </summary>
        public static Action<IDictionary<TKey, TValue>, TKey, TValue> IndexerSetter => Indexer<TKey, TValue>.Setter;
    }
    
    private static class Indexer<TKey, TValue>
    {
        public static readonly Func<IReadOnlyDictionary<TKey, TValue>, TKey, TValue> ReadOnly;
        
        public static readonly Func<IDictionary<TKey, TValue>, TKey, TValue> Getter;
        
        public static readonly Action<IDictionary<TKey, TValue>, TKey, TValue> Setter;

        static Indexer()
        {
            Ldtoken(PropertyGet(Type<IReadOnlyDictionary<TKey, TValue>>(), CollectionType.ItemIndexerName));
            Pop(out RuntimeMethodHandle method);
            Ldtoken(Type<IReadOnlyDictionary<TKey, TValue>>());
            Pop(out RuntimeTypeHandle type);
            ReadOnly = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Func<IReadOnlyDictionary<TKey, TValue>, TKey, TValue>>();

            Ldtoken(PropertyGet(Type<IDictionary<TKey, TValue>>(), CollectionType.ItemIndexerName));
            Pop(out method);
            Ldtoken(Type<IDictionary<TKey, TValue>>());
            Pop(out type);
            Getter = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Func<IDictionary<TKey, TValue>, TKey, TValue>>();

            Ldtoken(PropertySet(Type<IDictionary<TKey, TValue>>(), CollectionType.ItemIndexerName));
            Pop(out method);
            Setter = ((MethodInfo)MethodBase.GetMethodFromHandle(method, type)!).CreateDelegate<Action<IDictionary<TKey, TValue>, TKey, TValue>>();
        }
    }
}