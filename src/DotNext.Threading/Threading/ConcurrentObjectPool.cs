using System;
using System.Collections.Generic;
using System.Threading;
using static System.Diagnostics.Contracts.Contract;
using System.Collections.ObjectModel;
using Monitor = System.Threading.Monitor;

namespace DotNext.Threading
{
	/// <summary>
	/// Provides concurrent object pool where
	/// object selection is thread-safe but not selected object.
	/// </summary>
	/// <typeparam name="T">Type of objects in the pool.</typeparam>
	public class ConcurrentObjectPool<T>
		where T : class
	{
		/// <summary>
		/// Represents locked object.
		/// </summary>
		/// <remarks>
		/// Object lock cannot be stored in fields
        /// or escape call stack, therefore, it is ref-struct.
		/// </remarks>
		public ref struct Rental
		{
			private readonly T lockedObject;

			internal Rental(T obj, out bool lockTaken)
			{
				lockTaken = Monitor.TryEnter(obj);
				lockedObject = lockTaken ? obj : null;
			}

			/// <summary>
			/// Releases object lock and return it into pool.
			/// </summary>
			public void Dispose()
			{
				if (lockedObject is null)
					throw new ObjectDisposedException(ExceptionMessages.ReleasedLock);
				Monitor.Exit(lockedObject);
				this = default;
			}

			/// <summary>
			/// Gets channel/model associated with this lock.
			/// </summary>
			/// <param name="lock">Lock container.</param>
			public static implicit operator T(Rental @lock) => @lock.lockedObject;
		}

		/// <summary>
		/// Read-only collection of objects in this pool.
		/// </summary>
		protected readonly IReadOnlyList<T> objects;
		private int counter;

		/// <summary>
		/// Initializes a new object pool.
		/// </summary>
		/// <param name="objects">Predefined objects to be available from the pool.</param>
		public ConcurrentObjectPool(IList<T> objects)
		{
            if (objects.Count == 0)
                throw new ArgumentException(ExceptionMessages.CollectionIsEmpty, nameof(objects));
			this.objects = new ReadOnlyCollection<T>(objects);
			counter = -1;
		}

		/// <summary>
		/// Select first unbusy object from pool, lock it and return it.
		/// </summary>
		/// <returns>First unbusy object locked for the caller thread.</returns>
		public Rental Rent()
		{
			//each thread must have its own spin awaiter
			for (SpinWait spinner; ; spinner.SpinOnce())
			{
                //apply selection using round-robin mechanism
                var index = counter.IncrementAndGet() % objects.Count;
				//lock selected object if possible
				var result = new Rental(objects[index], out var locked);
				if (locked)
					return result;
			}
		}
	}
}
