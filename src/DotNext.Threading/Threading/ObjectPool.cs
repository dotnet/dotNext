using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Monitor = System.Threading.Monitor;

namespace DotNext.Threading
{
    /// <summary>
    /// Provides concurrent object pool where object selection is thread-safe except the rented object. 
    /// </summary>
    public abstract class ObjectPool : Disposable
    {
        private int cursor;

        private protected ObjectPool() => cursor = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected int NextIndex() => MakeIndex(cursor.IncrementAndGet(), Capacity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int MakeIndex(int cursor, int count) => (cursor & int.MaxValue) % count;

        private protected int Cursor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => cursor.VolatileWrite(value);
        }

        /// <summary>
        /// Gets total count of objects in this pool.
        /// </summary>
        public abstract int Capacity { get; }

        /// <summary>
        /// Gets number of rented objects.
        /// </summary>
        public virtual int Occupation => MakeIndex(cursor.VolatileRead(), Capacity) + 1;
    }

    /// <summary>
    /// Provides concurrent object pool where object selection is thread-safe except the rented object.
    /// </summary>
    /// <typeparam name="T">Type of objects in the pool.</typeparam>
    public class ObjectPool<T> : ObjectPool
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
            /// Returns object to the pool.
            /// </summary>
            public void Dispose()
            {
                if (lockedObject is null)
                    throw new ObjectDisposedException(ExceptionMessages.ReleasedLock);
                Monitor.Exit(lockedObject);
                this = default;
            }

            /// <summary>
            /// Gets object from the pool associated with this lock.
            /// </summary>
            /// <param name="lock">Lock container.</param>
            public static implicit operator T(Rental @lock) => @lock.lockedObject;
        }

        /// <summary>
        /// Read-only collection of objects in this pool.
        /// </summary>
        [SuppressMessage("Design", "CA1051", Justification = "Field is protected and its object cannot be modified")]
        protected readonly IReadOnlyList<T> objects;

        /// <summary>
        /// Initializes a new object pool.
        /// </summary>
        /// <param name="objects">Predefined objects to be available from the pool.</param>
        public ObjectPool(IList<T> objects)
        {
            if (objects.Count == 0)
                throw new ArgumentException(ExceptionMessages.CollectionIsEmpty, nameof(objects));
            this.objects = new ReadOnlyCollection<T>(objects);
        } 

        /// <summary>
        /// Gets total count of objects in this pool.
        /// </summary>
        public sealed override int Capacity => objects.Count;

        /// <summary>
        /// Select first unbusy object from pool, lock it and return it.
        /// </summary>
        /// <returns>First unbusy object locked for the caller thread.</returns>
        public Rental Rent()
        {
            //each thread must have its own spin awaiter
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                //lock selected object if possible
                var result = new Rental(objects[NextIndex()], out var locked);
                if (locked)
                    return result;
            }
        }
    }
}
