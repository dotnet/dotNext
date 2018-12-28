using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Cheats.Reflection
{
	internal abstract class Cache<K, T, V>
		where V: class
	{
		private readonly Dictionary<K, V> elements;
		private readonly ReaderWriterLockSlim syncObject;
		private readonly Func<T, K> keyResolver;

		private protected Cache(Func<T, K> keyResolver, IEqualityComparer<K> comparer)
		{
			elements = new Dictionary<K, V>(comparer);
			syncObject = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			this.keyResolver = keyResolver;
		}

		private protected Cache(Func<T, K> keyResolver)
			: this(keyResolver, EqualityComparer<K>.Default)
		{
		}

		private protected abstract V Create(T input);

		private V GetOrCreate(T input, K cacheKey)
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
					elements[cacheKey] = item = Create(input);
				}
				finally
				{
					syncObject.ExitWriteLock();
					syncObject.ExitUpgradeableReadLock();
				}
				return item;
			}
		}

		internal V GetOrCreate(T cacheKey)
		{
			syncObject.EnterReadLock();
			var exists = elements.TryGetValue(keyResolver(cacheKey), out var item);
			syncObject.ExitReadLock();
			if (exists) return item;
			//non-fast path, discover item
			syncObject.EnterUpgradeableReadLock();
			exists = elements.TryGetValue(keyResolver(cacheKey), out item);
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
					elements[keyResolver(cacheKey)] = item = Create(cacheKey);
				}
				finally
				{
					syncObject.ExitWriteLock();
					syncObject.ExitUpgradeableReadLock();
				}
				return item;
			}
		}
	}

	internal abstract class Cache<K, V>: Cache<K, K, V>
		where V: class
	{

		private protected Cache(IEqualityComparer<K> comparer)
			: base(Identity, comparer)
		{
		}

		private protected Cache()
			: base(Identity)
		{
		}

		private static K Identity(K key) => key;
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
