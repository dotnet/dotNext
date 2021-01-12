Async Lazy
====
[AsyncLazy&lt;T&gt;](../../api/DotNext.Threading.AsyncLazy-1.yml) provides support for asynchronous lazy initialization. This is asynchronous alternative to [Lazy&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.lazy-1) from .NET library. 

This class is useful when some object requires resource-intensive asynchronous initialization. Additionally, it is possible to erase initialized object and force initialization again using `Reset` method. To do that, `AsyncLazy` should be created as _resettable_ object through passing appropriate argument into its constructor.

The following example demonstrates how to use asynchrous lazy initialization:
```csharp
using DotNext.Threading;

class DataRepository
{
	private readonly AsyncLazy<DataCache> cache;
	private readonly IDbConnection connection;
	
	public DataRepository(IDbConnection connection)
	{
		this.connection = connection;
		cache = new AsyncLazy<DataCache>(() => InitCache(connection));
	}

	private static async Task<DataCache> InitCache(IDbConnection connection)
	{
		//initialize cache asynchronously
	}

	public async Task<User> GetUserById(long id)
	{
		var cache = await cache;
		if(cache.ContainsUser(id))
			return cache.GetUser(id);
		else
			return GetUserByIdNonCached(connection, id);
	}

	private static User GetUserByIdNonCached(IDbConnection connection, long id)
	{
		//database access
	}
}
```