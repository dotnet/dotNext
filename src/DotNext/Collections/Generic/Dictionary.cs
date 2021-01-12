using System;
using System.Collections.Generic;
using System.Reflection;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Collections.Generic
{
    using static Reflection.CollectionType;

    /// <summary>
    /// Represents various extensions for types <see cref="Dictionary{TKey, TValue}"/>
    /// and <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    public static class Dictionary
    {
        /// <summary>
        /// Provides strongly-typed access to dictionary indexer.
        /// </summary>
        /// <typeparam name="TKey">Type of keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">Type of values in the dictionary.</typeparam>
        public static class Indexer<TKey, TValue>
        {
            /// <summary>
            /// Represents read-only dictionary indexer.
            /// </summary>
            public static Func<IReadOnlyDictionary<TKey, TValue>, TKey, TValue> ReadOnly { get; }

            /// <summary>
            /// Represents dictionary value getter.
            /// </summary>
            public static Func<IDictionary<TKey, TValue>, TKey, TValue> Getter { get; }

            /// <summary>
            /// Represents dictionary value setter.
            /// </summary>
            public static Action<IDictionary<TKey, TValue>, TKey, TValue> Setter { get; }

            static Indexer()
            {
                Ldtoken(PropertyGet(Type<IReadOnlyDictionary<TKey, TValue>>(), ItemIndexerName));
                Pop(out RuntimeMethodHandle method);
                Ldtoken(Type<IReadOnlyDictionary<TKey, TValue>>());
                Pop(out RuntimeTypeHandle type);
                ReadOnly = DelegateHelpers.CreateDelegate<Func<IReadOnlyDictionary<TKey, TValue>, TKey, TValue>>((MethodInfo)MethodBase.GetMethodFromHandle(method, type));

                Ldtoken(PropertyGet(Type<IDictionary<TKey, TValue>>(), ItemIndexerName));
                Pop(out method);
                Ldtoken(Type<IDictionary<TKey, TValue>>());
                Pop(out type);
                Getter = DelegateHelpers.CreateDelegate<Func<IDictionary<TKey, TValue>, TKey, TValue>>((MethodInfo)MethodBase.GetMethodFromHandle(method, type));

                Ldtoken(PropertySet(Type<IDictionary<TKey, TValue>>(), ItemIndexerName));
                Pop(out method);
                Setter = DelegateHelpers.CreateDelegate<Action<IDictionary<TKey, TValue>, TKey, TValue>>((MethodInfo)MethodBase.GetMethodFromHandle(method, type));
            }
        }

        /// <summary>
        /// Returns <see cref="IReadOnlyDictionary{TKey, TValue}.get_Item"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Read-only dictionary instance.</param>
        /// <returns>A delegate representing dictionary indexer.</returns>
        public static Func<TKey, TValue> IndexerGetter<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary)
        {
            Push(dictionary);
            Dup();
            Ldvirtftn(PropertyGet(Type<IReadOnlyDictionary<TKey, TValue>>(), ItemIndexerName));
            Newobj(Constructor(Type<Func<TKey, TValue>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<TKey, TValue>>();
        }

        /// <summary>
        /// Returns <see cref="IReadOnlyDictionary{TKey, TValue}.Keys"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Read-only dictionary instance.</param>
        /// <returns>A delegate providing access to dictionary keys.</returns>
        public static Func<IEnumerable<TKey>> KeysGetter<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary)
        {
            Push(dictionary);
            Dup();
            Ldvirtftn(PropertyGet(Type<IReadOnlyDictionary<TKey, TValue>>(), nameof(IReadOnlyDictionary<TKey, TValue>.Keys)));
            Newobj(Constructor(Type<Func<IEnumerable<TKey>>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<IEnumerable<TKey>>>();
        }

        /// <summary>
        /// Returns <see cref="IReadOnlyDictionary{TKey, TValue}.Values"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Read-only dictionary instance.</param>
        /// <returns>A delegate providing access to dictionary keys.</returns>
        public static Func<IEnumerable<TValue>> ValuesGetter<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary)
        {
            Push(dictionary);
            Dup();
            Ldvirtftn(PropertyGet(Type<IReadOnlyDictionary<TKey, TValue>>(), nameof(IReadOnlyDictionary<TKey, TValue>.Values)));
            Newobj(Constructor(Type<Func<IEnumerable<TKey>>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<IEnumerable<TValue>>>();
        }

        /// <summary>
        /// Returns <see cref="IDictionary{TKey, TValue}.get_Item"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Mutable dictionary instance.</param>
        /// <returns>A delegate representing dictionary indexer.</returns>
        public static Func<TKey, TValue> IndexerGetter<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            Push(dictionary);
            Dup();
            Ldvirtftn(PropertyGet(Type<IDictionary<TKey, TValue>>(), ItemIndexerName));
            Newobj(Constructor(Type<Func<TKey, TValue>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<TKey, TValue>>();
        }

        /// <summary>
        /// Returns <see cref="IDictionary{TKey, TValue}.set_Item"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Mutable dictionary instance.</param>
        /// <returns>A delegate representing dictionary indexer.</returns>
        public static Action<TKey, TValue> IndexerSetter<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            Push(dictionary);
            Dup();
            Ldvirtftn(PropertySet(Type<IDictionary<TKey, TValue>>(), ItemIndexerName));
            Newobj(Constructor(Type<Action<TKey, TValue>>(), Type<object>(), Type<IntPtr>()));
            return Return<Action<TKey, TValue>>();
        }

        /// <summary>
        /// Returns <see cref="IDictionary{TKey, TValue}.Keys"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Read-only dictionary instance.</param>
        /// <returns>A delegate providing access to dictionary keys.</returns>
        public static Func<ICollection<TKey>> KeysGetter<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            Push(dictionary);
            Dup();
            Ldvirtftn(PropertyGet(Type<IDictionary<TKey, TValue>>(), nameof(IDictionary<TKey, TValue>.Keys)));
            Newobj(Constructor(Type<Func<ICollection<TKey>>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<ICollection<TKey>>>();
        }

        /// <summary>
        /// Returns <see cref="IDictionary{TKey, TValue}.Values"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Read-only dictionary instance.</param>
        /// <returns>A delegate providing access to dictionary keys.</returns>
        public static Func<ICollection<TValue>> ValuesGetter<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            Push(dictionary);
            Dup();
            Ldvirtftn(PropertyGet(Type<IDictionary<TKey, TValue>>(), nameof(IDictionary<TKey, TValue>.Values)));
            Newobj(Constructor(Type<Func<ICollection<TKey>>>(), Type<object>(), Type<IntPtr>()));
            return Return<Func<ICollection<TValue>>>();
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary if the key does not exist.
        /// </summary>
        /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
        /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="key">The key of the key-value pair.</param>
        /// <returns>
        /// The corresponding value in the dictionary if <paramref name="key"/> already exists,
        /// or a new instance of <typeparamref name="TValue"/>.
        /// </returns>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
            where TValue : new()
        {
            if (!dictionary.TryGetValue(key, out var value))
                dictionary.Add(key, value = new TValue());
            return value;
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary if the key does not exist.
        /// </summary>
        /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
        /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="key">The key of the key-value pair.</param>
        /// <param name="value">The value of the key-value pair.</param>
        /// <returns>
        /// The corresponding value in the dictionary if <paramref name="key"/> already exists,
        /// or <paramref name="value"/>.
        /// </returns>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.TryGetValue(key, out var temp))
                value = temp;
            else
                dictionary.Add(key, value);
            return value;
        }

        /// <summary>
        /// Generates a value and adds the key-value pair to the dictionary if the key does not
        /// exist.
        /// </summary>
        /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
        /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="key">The key of the key-value pair.</param>
        /// <param name="valueFactory">
        /// The function used to generate the value from the key.
        /// </param>
        /// <returns>
        /// The corresponding value in the dictionary if <paramref name="key"/> already exists,
        /// or the value generated by <paramref name="valueFactory"/>.
        /// </returns>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, in ValueFunc<TKey, TValue> valueFactory)
        {
            if (dictionary.TryGetValue(key, out var value))
                return value;
            value = valueFactory.Invoke(key);
            dictionary.Add(key, value);
            return value;
        }

        /// <summary>
        /// Generates a value and adds the key-value pair to the dictionary if the key does not
        /// exist.
        /// </summary>
        /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
        /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="key">The key of the key-value pair.</param>
        /// <param name="valueFactory">
        /// The function used to generate the value from the key.
        /// </param>
        /// <returns>
        /// The corresponding value in the dictionary if <paramref name="key"/> already exists,
        /// or the value generated by <paramref name="valueFactory"/>.
        /// </returns>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueFactory)
            => GetOrAdd(dictionary, key, new ValueFunc<TKey, TValue>(valueFactory, true));

        /// <summary>
        /// Applies specific action to each dictionary.
        /// </summary>
        /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
        /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="action">The action to be applied for each key/value pair.</param>
        public static void ForEach<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, in ValueAction<TKey, TValue> action)
        {
            foreach (var (key, value) in dictionary)
                action.Invoke(key, value);
        }

        /// <summary>
        /// Applies specific action to each dictionary.
        /// </summary>
        /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
        /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="action">The action to be applied for each key/value pair.</param>
        public static void ForEach<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Action<TKey, TValue> action)
            => ForEach(dictionary, new ValueAction<TKey, TValue>(action, true));

        /// <summary>
        /// Gets dictionary value by key if it exists or
        /// invoke <paramref name="defaultValue"/> and
        /// return its result as a default value.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="key">A key associated with the value.</param>
        /// <param name="defaultValue">A delegate to be invoked if key doesn't exist in the dictionary.</param>
        /// <returns>The value associated with the key or returned by the delegate.</returns>
        public static TValue GetOrInvoke<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, in ValueFunc<TValue> defaultValue)
            => dictionary.TryGetValue(key, out var value) ? value : defaultValue.Invoke();

        /// <summary>
        /// Gets dictionary value by key if it exists or
        /// invoke <paramref name="defaultValue"/> and
        /// return its result as a default value.
        /// </summary>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="key">A key associated with the value.</param>
        /// <param name="defaultValue">A delegate to be invoked if key doesn't exist in the dictionary.</param>
        /// <returns>The value associated with the key or returned by the delegate.</returns>
        public static TValue GetOrInvoke<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> defaultValue)
            => GetOrInvoke(dictionary, key, new ValueFunc<TValue>(defaultValue, true));

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="key">The key whose value to get.</param>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <returns>The optional value associated with the key.</returns>
        public static Optional<TValue> TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            => dictionary.TryGetValue(key, out var value) ? new Optional<TValue>(value) : Optional<TValue>.None;

        /// <summary>
        /// Removes the value with the specified key and return the removed value.
        /// </summary>
        /// <param name="dictionary">A dictionary to modify.</param>
        /// <param name="key">The key of the element to remove.</param>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <returns>The removed value.</returns>
        public static Optional<TValue> TryRemove<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            => dictionary.TryGetValue(key, out var value) && dictionary.Remove(key) ? new Optional<TValue>(value) : Optional<TValue>.None;

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="key">The key whose value to get.</param>
        /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of dictionary values.</typeparam>
        /// <returns>The optional value associated with the key.</returns>
        public static Optional<TValue> TryGetValue<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
            => dictionary.TryGetValue(key, out var value) ? new Optional<TValue>(value) : Optional<TValue>.None;

        /// <summary>
        /// Applies lazy conversion for each dictionary value.
        /// </summary>
        /// <typeparam name="TKey">Type of keys.</typeparam>
        /// <typeparam name="TValue">Type of values.</typeparam>
        /// <typeparam name="TResult">Type of mapped values.</typeparam>
        /// <param name="dictionary">A dictionary to be mapped.</param>
        /// <param name="mapper">Mapping function.</param>
        /// <returns>Read-only view of the dictionary where each value is converted in lazy manner.</returns>
        public static ReadOnlyDictionaryView<TKey, TValue, TResult> ConvertValues<TKey, TValue, TResult>(this IReadOnlyDictionary<TKey, TValue> dictionary, in ValueFunc<TValue, TResult> mapper)
            => new ReadOnlyDictionaryView<TKey, TValue, TResult>(dictionary, mapper);

        /// <summary>
        /// Applies lazy conversion for each dictionary value.
        /// </summary>
        /// <typeparam name="TKey">Type of keys.</typeparam>
        /// <typeparam name="TValue">Type of values.</typeparam>
        /// <typeparam name="TResult">Type of mapped values.</typeparam>
        /// <param name="dictionary">A dictionary to be mapped.</param>
        /// <param name="mapper">Mapping function.</param>
        /// <returns>Read-only view of the dictionary where each value is converted in lazy manner.</returns>
        public static ReadOnlyDictionaryView<TKey, TValue, TResult> ConvertValues<TKey, TValue, TResult>(this IReadOnlyDictionary<TKey, TValue> dictionary, Converter<TValue, TResult> mapper)
            => ConvertValues(dictionary, mapper.AsValueFunc(true));
    }
}
