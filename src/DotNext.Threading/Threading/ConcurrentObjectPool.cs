using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Provides concurrent object pool where object selection is thread-safe except the rented object.
    /// </summary>
    /// <typeparam name="T">Type of objects in the pool.</typeparam>
    public class ConcurrentObjectPool<T> : Disposable
        where T : class
    {
        /// <summary>
        /// Represents rented object.
        /// </summary>
        /// <remarks>
        /// Call <see cref="IDisposable.Dispose"/> to return object back to the pool.
        /// </remarks>
        public interface IRental : IDisposable
        {
            /// <summary>
            /// Gets rented object.
            /// </summary>
            T Resource { get; }
        }

        private sealed class Rental : IRental
        {
            //cached delegate to avoid memory allocations and increase chance of inline caching
            private static readonly WaitCallback DisposeResource = resource => (resource as IDisposable)?.Dispose();
            private AtomicBoolean lockState;
            private AtomicReference<T> resource;
            internal readonly int Index;
            private readonly long maxWeight;
            private long weight;

            internal Rental(int index, long weight, T resource = null)
            {
                Index = index;
                this.resource = new AtomicReference<T>(resource);
                this.weight = maxWeight = weight;
            }

            internal event Action<Rental> Released;

            T IRental.Resource => resource.Value;

            internal void InitResource(Func<T> factory) => resource.SetIfNull(factory);

            internal bool TryAcquire() => lockState.FalseToTrue();

            //this method indicates that the object is requested
            //and no longer starving
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Touch() => weight.VolatileWrite(maxWeight);

            //used in SFG mode only
            internal bool Starve()
            {
                bool success;
                if (success = lockState.FalseToTrue())
                {
                    if(success = weight.DecrementAndGet() <= 0)
                    {
                        var resource = this.resource.GetAndSet(null);
                        //prevent this method from blocking so dispose resource asynchronously
                        if(!(resource is null))
                            ThreadPool.QueueUserWorkItem(DisposeResource, resource);
                    }
                    lockState.Value = false;
                }
                return success;
            }

            void IDisposable.Dispose()
            {
                lockState.Value = false;
                Released?.Invoke(this);
            }

            internal void Destroy(bool disposeResource)
            {
                Released = null;
                var resource = this.resource.GetAndSet(null);
                if (disposeResource && resource is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        //cached delegate to avoid allocations
        private static readonly Func<Rental, Rental, Rental> SelectLastRenal = (current, update) => current is null || update.Index > current.Index ? update : current;
        private readonly Func<T> factory;
        private AtomicReference<Rental> last;
        private readonly bool lazyInstantiation;
        private int cursor, pressure;
        private readonly Rental[] objects;
        

        private ConcurrentObjectPool(int capacity, Func<int, Rental> initializer, bool fairness)
        {
            var objects = new Rental[capacity];
            var callback = fairness ? new Action<Rental>(ReleasedRR) : new Action<Rental>(ReleasedSJF);
            for (var i = 0; i < capacity; i++)
            {
                ref Rental rental = ref objects[i];
                rental = initializer(i);
                rental.Released += callback;
            }
            cursor = -1;
            pressure = 0;
        }

        public ConcurrentObjectPool(int capacity, Func<T> factory)
            : this(capacity, index => new Rental(index, capacity * 2L), true)
        {
            lazyInstantiation = true;
            this.factory = factory;
        }

        public ConcurrentObjectPool(IList<T> objects)
            : this(objects.Count, index => new Rental(index, 0, objects[index]), false)
        {
            lazyInstantiation = false;
        }

        //release object according with Round-robin scheduling algorithm
        private void ReleasedRR(Rental rental) => pressure.DecrementAndGet();

        //release object according with Shortest Job First algorithm
        private void ReleasedSJF(Rental rental)
        {
            cursor.VolatileWrite(rental.Index - 1); //set cursor to the released object
            pressure.DecrementAndGet();
            rental = last.AccumulateAndGet(rental, SelectLastRenal);
            //starvation detected, disposed object stored in rental object
            if (rental.Starve())
                last.Value = rental.Index == 0 ? null : objects[rental.Index - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NextIndex() => MakeIndex(cursor.IncrementAndGet(), Capacity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int MakeIndex(int cursor, int count) => (cursor & int.MaxValue) % count;

        /// <summary>
        /// Gets total count of objects in this pool.
        /// </summary>
        public int Capacity => objects.Length;

        /// <summary>
        /// Gets number of rented objects.
        /// </summary>
        public int Pressure => MakeIndex(pressure.VolatileRead(), Capacity) + 1;

        /// <summary>
        /// Rents the object from this pool.
        /// </summary>
        /// <returns>The object allows to control lifetime of the rent.</returns>
        public IRental Rent()
        {
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                var rental = objects[NextIndex()];
                if (rental.TryAcquire())
                {
                    rental.Touch();
                    if (!(factory is null))
                        rental.InitResource(factory);
                    pressure.IncrementAndGet();
                    return rental;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (var i = 0; i < objects.Length; i++)
                {
                    ref Rental rental = ref objects[i];
                    rental.Destroy(lazyInstantiation);
                    rental = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
