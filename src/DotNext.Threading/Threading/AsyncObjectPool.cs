using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    public class AsyncObjectPool<T> : ObjectPool
        where T : class
    {
        public delegate ValueTask<T> ObjectFactory();

        internal interface IRental
        {
            T Resource { get; }
            void Release(Action<int> callback);
        }

        public struct Rental : IDisposable
        {
            private readonly IRental rental;
            private readonly Action<int> callback;

            internal Rental(IRental rental, Action<int> callback)
            {
                this.rental = rental;
                this.callback = callback;
            }

            /// <summary>
            /// Returns the object to the pool.
            /// </summary>
            public void Dispose()
            {
                if(rental is null)
                    throw new ObjectDisposedException(ExceptionMessages.ReleasedLock);
                rental.Release(callback);
                this = default;
            }

            public static implicit operator T(in Rental rental) => rental.rental?.Resource;
        }

        private sealed class RentalLock : AsyncExclusiveLock, IRental
        {
            private readonly int index;
            private volatile T resource;

            internal RentalLock(int index) => this.index = index;

            public T Resource => resource;

            internal async ValueTask InitResource(ObjectFactory factory)
            {
                if(resource is null)
                    resource = await factory();
            }

            protected override void Dispose(bool disposing)
            {
                if(disposing)
                {
                    (resource as IDisposable)?.Dispose();
                    resource = null;
                }
                base.Dispose(disposing);
            }
        
            void IRental.Release(Action<int> callback)
            {
                Release();
                callback(index);
            }
        }

        private readonly RentalLock[] objects;
        private readonly ObjectFactory factory;
        private Action<int> releaseCallback;
        private int occupation;

        public AsyncObjectPool(int capacity, ObjectFactory factory)
        {
            if(capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            objects = new RentalLock[capacity];
            objects.ForEach(CreateRentalLock);
            releaseCallback = Released;
        }

        private static void CreateRentalLock(long index, ref RentalLock rental)
            => rental = new RentalLock((int)index);

        private void Released(int index)
        {
            Cursor = index - 1; //set cursor to the released object
            occupation.DecrementAndGet();
        }

        /// <summary>
        /// Gets total count of objects in this pool.
        /// </summary>
        public sealed override int Capacity => objects.Length;

        /// <summary>
        /// Gets number of rented objects.
        /// </summary>
        public sealed override int Occupation => occupation;

        public async ValueTask<Rental> Rent()
        {
            while(true)
            {
                var index = NextIndex();
                var rental = objects[index];
                if(await rental.TryAcquire(TimeSpan.Zero).ConfigureAwait(false))
                {
                    occupation.IncrementAndGet();
                    await rental.InitResource(factory);
                    return new Rental(rental, releaseCallback);
                }
                else if(index == objects.LongLength - 1L)
                    await Task.Yield();
                else
                    continue;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                objects.ForEach((long index, ref RentalLock rental) =>
                {
                    rental.Dispose();
                    rental = null;
                });
                releaseCallback = null;
            }
            base.Dispose(disposing);
        }
    }
}