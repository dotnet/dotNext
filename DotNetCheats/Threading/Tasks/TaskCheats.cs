using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cheats.Threading.Tasks
{
	public static class TaskCheats
	{
		public static R GetResult<R>(this Task<R> task, TimeSpan timeout)
		{
			if (task.Wait(timeout))
				return task.Result;
			else
				throw new TimeoutException();
		}

		public static R GetResult<R>(this Task<R> task, CancellationToken token)
		{
			task.Wait(token);
			return task.Result;
		}
	}
}
