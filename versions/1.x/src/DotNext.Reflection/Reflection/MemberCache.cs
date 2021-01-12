using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DotNext.Reflection
{
    using ReaderWriterSpinLock = Threading.ReaderWriterSpinLock;

    internal abstract class Cache<K, V>
        where V : class
    {
        /*
         * Can't use ConcurrentDictionary here because GetOrAdd method can call factory multiple times for the same key
         */
        private readonly IDictionary<K, V> elements;
        private ReaderWriterSpinLock syncObject;

        private protected Cache(IEqualityComparer<K> comparer) => elements = new Dictionary<K, V>(comparer);

        private protected Cache()
            : this(EqualityComparer<K>.Default)
        {
        }

        private protected abstract V Create(K cacheKey);

        internal V GetOrCreate(K cacheKey)
        {
            syncObject.EnterReadLock();
            var exists = elements.TryGetValue(cacheKey, out var item);
            syncObject.ExitReadLock();
            if (exists)
                goto exit;
            //non-fast path, discover item
            syncObject.EnterWriteLock();
            if (elements.TryGetValue(cacheKey, out item))
                syncObject.ExitWriteLock();
            else
                try
                {
                    item = Create(cacheKey);
                    if (item != null)
                        elements.Add(cacheKey, item);
                }
                finally
                {
                    syncObject.ExitWriteLock();
                }
            exit:
            return item;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct MemberKey : IEquatable<MemberKey>
    {
        internal readonly bool NonPublic;
        internal readonly string Name;

        internal MemberKey(string name, bool nonPublic)
        {
            NonPublic = nonPublic;
            Name = name;
        }

        public bool Equals(MemberKey other) => NonPublic == other.NonPublic && Name == other.Name;

        public override bool Equals(object other) => other is MemberKey key && Equals(key);

        public override int GetHashCode()
        {
            var hashCode = -910176598;
            hashCode = hashCode * -1521134295 + NonPublic.GetHashCode();
            hashCode = hashCode * -1521134295 + Name?.GetHashCode() ?? 0;
            return hashCode;
        }
    }

    internal abstract class MemberCache<M, E> : Cache<MemberKey, E>
        where M : MemberInfo
        where E : class, IMember<M>
    {
        private static readonly UserDataSlot<MemberCache<M, E>> Slot = UserDataSlot<MemberCache<M, E>>.Allocate();

        internal E GetOrCreate(string memberName, bool nonPublic) => GetOrCreate(new MemberKey(memberName, nonPublic));

        private protected abstract E Create(string memberName, bool nonPublic);

        private protected sealed override E Create(MemberKey key) => Create(key.Name, key.NonPublic);

        internal static MemberCache<M, E> Of<C>(MemberInfo member)
            where C : MemberCache<M, E>, new()
            => member.GetUserData().GetOrSet<MemberCache<M, E>, C>(Slot);
    }
}
