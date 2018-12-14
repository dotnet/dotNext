using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace MissingPieces.Metaprogramming
{
	internal abstract class MemberCache<M, E>
		where M: MemberInfo
		where E: IMember<M>
	{
		private readonly Dictionary<string, E> members;
		private readonly ReaderWriterLockSlim syncObject;

		internal MemberCache()
		{
			members = new Dictionary<string, E>();
			syncObject = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
		}

		private protected abstract E CreateMember(string memberName);

		internal E GetOrCreate(string memberName)
		{
			syncObject.EnterReadLock();
			var exists = members.TryGetValue(memberName, out var member);
			syncObject.ExitReadLock();
			if (exists) return member;
			//non-fast path, discover member
			syncObject.EnterUpgradeableReadLock();
			exists = members.TryGetValue(memberName, out member);
			if (exists)
			{
				syncObject.ExitUpgradeableReadLock();
				return member;
			}
			else
			{
				syncObject.EnterWriteLock();
				try
				{
					members[memberName] = member = CreateMember(memberName);
				}
				finally
				{
					syncObject.ExitWriteLock();
					syncObject.ExitUpgradeableReadLock();
				}
				return member;
			}
		}
	}
}
