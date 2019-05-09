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
    public class ObjectPool<T> : Disposable
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
            private AtomicBoolean lockState;
            private volatile T resource;
            internal readonly int Index;
            private readonly int maxWeight;
            private int weight;

            internal Rental(int index, int weight, T resource = null)
            {
                Index = index;
                this.resource = resource;
                this.weight = maxWeight = weight;
            }

            internal event Action<Rental> Released;

            T IRental.Resource => resource;

            internal void InitResource(Func<T> factory)
            {
                if (resource is null)
                    resource = factory();
            }

            internal bool TryAcquire()
            {
                return lockState.FalseToTrue();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Touch() => weight.VolatileWrite(maxWeight);

            internal bool Starve()
            {
                var success = false;
                if (TryAcquire() && weight.DecrementAndGet() == 0)
                    (resource as IDisposable).Dispose();
                lockState.Value = false;    //release lock
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
                if (disposeResource && resource is IDisposable disposable)
                    disposable.Dispose();
                resource = null;
            }
        }

        private readonly Func<T> factory;
        private AtomicReference<Rental> last;
        private readonly bool lazyInstantiation;
        private int cursor, pressure;
        private readonly Rental[] objects;
        //cached delegate to avoid allocations
        private readonly Func<Rental, Rental, Rental> lastRentalSelector;

        private ObjectPool(int capacity, Func<int, Rental> initializer, bool fairness)
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
            lastRentalSelector = SelectLastRental;
        }

        private protected ObjectPool(int capacity, Func<T> factory)
            : this(capacity, index => new Rental(index, capacity), true)
        {
            lazyInstantiation = true;
            this.factory = factory;
        }

        private protected ObjectPool(IList<T> objects)
            : this(objects.Count, index => new Rental(index, objects.Count, objects[index]), false)
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
            rental = last.AccumulateAndGet(rental, lastRentalSelector);
            //starvation detected, disposed object stored in rental object
            if (rental.Starve())
                last.Value = rental.Index == 0 ? null : objects[rental.Index - 1];
        }

        private static Rental SelectLastRental(Rental current, Rental update)
        {
            if (current is null)
                return update;
            else if (update.Index > current.Index)
                return update;
            else
                return current;
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
