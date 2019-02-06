using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace DotNext.Reflection
{
	internal abstract class Cache<K, V>
		where V: class
	{
        private readonly Dictionary<K, V> elements;
        private readonly ReaderWriterLockSlim syncObject;

        private protected Cache(IEqualityComparer<K> comparer)
        {
            elements = new Dictionary<K, V>(comparer);
            syncObject = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

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
            if (exists) return item;
            //non-fast path, discover item
            syncObject.EnterUpgradeableReadLock();
            exists = elements.TryGetValue(cacheKey, out item);
            if (exists)
            {
                syncObject.ExitUpgradeableReadLock();
                return item;
            }
            else
            {
                syncObject.EnterWriteLock();
                try
                {
                    return elements[cacheKey] = item = Create(cacheKey);
                }
                finally
                {
                    syncObject.ExitWriteLock();
                    syncObject.ExitUpgradeableReadLock();
                }
            }
        }
    }

	internal abstract class MemberCache<M, E>: Cache<string, E>
		where M: MemberInfo
		where E: class, IMember<M>
	{
		private protected MemberCache()
			: base(StringComparer.Ordinal)
		{
		}
	}
}
