using System;
using System.Collections.Generic;
using System.Threading;
using static System.Diagnostics.Contracts.Contract;
using System.Collections.ObjectModel;
using Monitor = System.Threading.Monitor;

namespace Cheats.Threading
{
	/// <summary>
	/// Provides concurrent object pool where
	/// object selection is thread-safe but selected object is not.
	/// </summary>
	/// <typeparam name="T">Type of objects in the pool.</typeparam>
	public class ConcurrentObjectPool<T>
		where T : class
	{
		/// <summary>
		/// Represents locked object.
		/// </summary>
		/// <remarks>
		/// Object lock should be lightweight
		/// therefore it is struct.
		/// </remarks>
		public ref struct Lock
		{
			private T lockedObject;

			internal Lock(T obj, out bool lockTaken)
			{
				lockTaken = false;
				Monitor.TryEnter(obj, ref lockTaken);
				lockedObject = lockTaken ? obj : null;
			}

			/// <summary>
			/// Releases object lock and return it into pool.
			/// </summary>
			public void Release()
			{
				if (lockedObject is null)
					throw new ObjectDisposedException("This lock is released");
				Monitor.Exit(lockedObject);
				lockedObject = null;
			}

			/// <summary>
			/// Gets channel/model associated with this lock.
			/// </summary>
			/// <param name="lock">Lock container.</param>
			public static implicit operator T(Lock @lock) => @lock.lockedObject;
		}

		/// <summary>
		/// Read-only collection of objects in this pool.
		/// </summary>
		protected readonly ReadOnlyCollection<T> objects;
		private int counter;

		/// <summary>
		/// Initializes a new object pool.
		/// </summary>
		/// <param name="objects">Predefined objects to be available from the pool.</param>
		public ConcurrentObjectPool(IList<T> objects)
		{
			objects = new ReadOnlyCollection<T>(objects);
			counter = -1;
		}

		/// <summary>
		/// Select first unbusy object from pool and lock it.
		/// </summary>
		/// <returns>First unbusy object locked for the caller thread.</returns>
		public Lock SelectAndLock()
		{
			Requires(objects.Count > 0, "This pool has no channels");
			//each thread must have its own spin awaiter
			for (SpinWait spinner; ; spinner.SpinOnce())
			{
				//apply selection using round-robin mechanism
				var index = Math.Abs(counter.IncrementAndGet() % objects.Count);
				//lock selected object if possible
				var result = new Lock(objects[index], out var locked);
				if (locked)
					return result;
			}
		}
	}
}
