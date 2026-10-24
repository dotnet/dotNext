using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

using Numerics;
using Threading;

public partial class RandomAccessCache<TKey, TValue>
{
    // devirtualize Value getter manually (JIT will replace this method with one of the actual branches)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static ref readonly TValue GetValue(KeyValuePair pair)
    {
        Debug.Assert(pair is not null);
        Debug.Assert(Interlocked.IsAtomic<TValue>() ? pair is KeyValuePairAtomicAccess : pair is KeyValuePairNonAtomicAccess);

        return ref Interlocked.IsAtomic<TValue>()
            ? ref Unsafe.As<KeyValuePairAtomicAccess>(pair).Value
            : ref Unsafe.As<KeyValuePairNonAtomicAccess>(pair).ValueRef;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetValue(KeyValuePair pair, TValue value)
    {
        if (Interlocked.IsAtomic<TValue>())
        {
            Unsafe.As<KeyValuePairAtomicAccess>(pair).Value = value;
        }
        else
        {
            Unsafe.As<KeyValuePairNonAtomicAccess>(pair).Value = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClearValue(KeyValuePair pair)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
        {
            // do nothing
        }
        else if (Interlocked.IsAtomic<TValue>())
        {
            Unsafe.As<KeyValuePairAtomicAccess>(pair).Value = default!;
        }
        else
        {
            Unsafe.As<KeyValuePairNonAtomicAccess>(pair).ClearValue();
        }
    }

    private static KeyValuePair CreatePair(TKey key, TValue value, int hashCode, bool willBePromoted)
        => Interlocked.IsAtomic<TValue>()
            ? new KeyValuePairAtomicAccess(key, hashCode, willBePromoted) { Value = value }
            : new KeyValuePairNonAtomicAccess(key, hashCode, willBePromoted) { Value = value };

    private volatile BucketList buckets;

    internal abstract partial class KeyValuePair : Command
    {
        internal readonly int KeyHashCode;
        internal readonly TKey Key;
        internal volatile KeyValuePair? NextInBucket; // volatile, used by the dictionary subsystem only
        
        // Reference counting is used to establish lifetime of the stored value (not KeyValuePair instance).
        // Initial value 1 means that the pair is referenced by the eviction queue. There
        // are two competing threads that may decrement the counter to zero: removal thread (see TryRemove)
        // and eviction thread. To synchronize the decision, 'cacheState' is used. The thread that evicts the pair
        // successfully (transition from 0 => -1) is able to decrement the counter to zero.
        // The promotion queue holds an extra reference until the eviction thread processes the pair.
        // The counter is shared with the copies of the pair created by resize, because the readers
        // may still hold the pair from the previous version of the bucket list.
        private readonly StrongBox<int> lifetimeCounter;

        protected KeyValuePair(TKey key, int hashCode, bool willBePromoted)
        {
            KeyHashCode = hashCode;
            Key = key;
            lifetimeCounter = new(1 + Unsafe.BitCast<bool, byte>(willBePromoted));
        }

        protected KeyValuePair(KeyValuePair other)
        {
            Key = other.Key;
            KeyHashCode = other.KeyHashCode;
            cacheState = other.cacheState;
            addedEvent = other.addedEvent;
            lifetimeCounter = other.lifetimeCounter;
        }

        public abstract KeyValuePair Clone();

        internal bool TryAcquireCounter()
        {
            ref var counter = ref lifetimeCounter.Value;
            int currentValue, tmp = Volatile.Read(ref counter);
            do
            {
                currentValue = tmp;
                if (currentValue is 0)
                    break;
            } while ((tmp = Interlocked.CompareExchange(ref counter, currentValue + 1, currentValue)) != currentValue);

            return currentValue > 0U;
        }

        internal bool ReleaseCounter() => Interlocked.Decrement(ref lifetimeCounter.Value) > 0;
        
        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal (int Alive, int Dead) BucketNodesCount
        {
            get
            {
                var alive = 0;
                var dead = 0;
                for (var current = this; current is not null; current = current.NextInBucket)
                {
                    ref var counterRef = ref current.IsDead ? ref dead : ref alive;
                    counterRef++;
                }

                return (alive, dead);
            }
        }
    }

    private sealed class KeyValuePairAtomicAccess : KeyValuePair
    {
        internal required TValue Value;

        public KeyValuePairAtomicAccess(TKey key, int hashCode, bool willBePromoted)
            : base(key, hashCode, willBePromoted)
        {
        }

        [SetsRequiredMembers]
        private KeyValuePairAtomicAccess(KeyValuePairAtomicAccess other)
            : base(other)
            => Value = other.Value;

        public override KeyValuePairAtomicAccess Clone() => new(this);

        public override string ToString() => ToString(Value);
    }

    // non-atomic access utilizes copy-on-write semantics
    private sealed class KeyValuePairNonAtomicAccess : KeyValuePair
    {
        private static readonly ValueHolder DefaultHolder = new(default!);
        
        private ValueHolder holder;
        
        public KeyValuePairNonAtomicAccess(TKey key, int hashCode, bool willBePromoted)
            : base(key, hashCode, willBePromoted)
        {
        }

        [SetsRequiredMembers]
#pragma warning disable CS8618
        private KeyValuePairNonAtomicAccess(KeyValuePairNonAtomicAccess other)
            : base(other)
            => holder = other.holder;
#pragma warning restore CS8618

        public override KeyValuePairNonAtomicAccess Clone() => new(this);

        internal required TValue Value
        {
            get => holder.Value;

            [MemberNotNull(nameof(holder))] set => holder = new(value);
        }

        internal ref readonly TValue ValueRef => ref holder.Value;

        internal void ClearValue() => holder = DefaultHolder;

        public override string ToString() => ToString(Value);
        
        private sealed class ValueHolder(TValue value)
        {
            internal readonly TValue Value = value;
        }
    }
    
    internal interface IKeyValuePairVisitor
    {
        public static abstract bool Visit(KeyValuePair pair);
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct NotDeadFilter : IKeyValuePairVisitor
    {
        static bool IKeyValuePairVisitor.Visit(KeyValuePair pair) => !pair.IsDead;
    }

    [DebuggerDisplay($"NumberOfItems = {{{nameof(Count)}}}, IsLockHeld = {{{nameof(IsLockHeld)}}}")]
    [StructLayout(LayoutKind.Auto)]
    internal struct Bucket
    {
        internal AsyncParkingLot.State LockState; // modified by AsyncParkingLot only
        private KeyValuePair? addedPair;
        private volatile KeyValuePair? first;
        private volatile int count;

        internal readonly KeyValuePair? First => first;
        
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal readonly int CollisionCount => count;

        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly (int Alive, int Dead) Count => first?.BucketNodesCount ?? default;

        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly bool IsLockHeld => LockState is not AsyncParkingLot.State.Free;

        internal KeyValuePair? TryAdd(TKey key, int hashCode, TValue value, bool willBePromoted)
        {
            KeyValuePair? result;
            if (addedPair is not null)
            {
                result = null;
            }
            else
            {
                Add(addedPair = result = CreatePair(key, value, hashCode, willBePromoted));
            }

            return result;
        }

        internal void Add(KeyValuePair pair)
        {
            // possible contention with TryRemove private method that can be called without lock
            for (KeyValuePair? current = first, tmp;; current = tmp)
            {
                pair.NextInBucket = current;
                tmp = Interlocked.CompareExchange(ref first, pair, current);
                if (ReferenceEquals(current, tmp))
                    break;
            }

            Interlocked.Increment(ref count);
        }

        internal void MarkAsReadyToAdd() => addedPair = null;

        internal Task MarkAsReadyToAddAndGetTask()
        {
            Task task;
            if (addedPair is not null)
            {
                task = addedPair.AddedEvent;
                addedPair = null;
            }
            else
            {
                task = Task.CompletedTask;
            }

            return task;
        }

        private void TryRemove(KeyValuePair? previous, KeyValuePair current)
        {
            // This method can be called concurrently by different threads. It doesn't guarantee removal of the pair.
            // For instance, we have a => b => c => d linked pair, 'b' and 'c' are dead and 2 threads trying to remove them
            // Thread #1 modifies a link a => b to a => c
            // Thread #2 modifies a link b => c to b => d
            // The produced linked list is a => c => d
            // As we can see, the list contains dead node 'c'. We are okay with that, because the list is inspected
            // on every access (read/write), so it will be eventually deleted
            ref var location = ref previous is null ? ref first : ref previous.NextInBucket;
            if (ReferenceEquals(Interlocked.CompareExchange(ref location, current.NextInBucket, current), current))
                Interlocked.Decrement(ref count);
        }

        internal KeyValuePair? TryRemove<TSelector>(TSelector selector, int hashCode)
            where TSelector : struct, IEquatable<TKey>, allows ref struct
        {
            var result = default(KeyValuePair?);

            // remove all dead nodes from the bucket
            for (KeyValuePair? current = first, previous = null;
                 current is not null;
                 previous = current, current = current.NextInBucket)
            {
                if (result is null && hashCode == current.KeyHashCode
                                   && selector.Equals(current.Key)
                                   && current.MarkAsEvicted())
                {
                    result = current;
                }

                if (current.IsDead)
                {
                    TryRemove(previous, current);
                }
            }

            return result;
        }

        internal KeyValuePair? TryGet<TSelector, TVisitor>(TSelector selector, int hashCode)
            where TSelector : struct, IEquatable<TKey>, allows ref struct
            where TVisitor : struct, IKeyValuePairVisitor, allows ref struct
        {
            var result = default(KeyValuePair?);

            // remove all dead nodes from the bucket
            for (KeyValuePair? current = first, previous = null;
                 current is not null;
                 previous = current, current = current.NextInBucket)
            {
                if (result is null && hashCode == current.KeyHashCode
                                   && selector.Equals(current.Key)
                                   && TVisitor.Visit(current))
                {
                    result = current;
                }

                if (current.IsDead)
                {
                    TryRemove(previous, current);
                }
            }

            return result;
        }

        internal void CleanUp()
        {
            // remove all dead nodes from the bucket
            for (KeyValuePair? current = first, previous = null;
                 current is not null;
                 previous = current, current = current.NextInBucket)
            {
                if (current.IsDead)
                {
                    TryRemove(previous, current);
                }
            }
        }
        
        internal void Invalidate(Action<KeyValuePair> cleanup)
        {
            for (KeyValuePair? current = first, previous = null;
                 current is not null;
                 previous = current, current = current.NextInBucket)
            {
                TryRemove(previous, current);
                    
                if (current.MarkAsDead())
                {
                    cleanup.Invoke(current);
                }
            }
        }
        
        public readonly Enumerator GetEnumerator() => new(first);
        
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator(KeyValuePair? current)
        {
            private bool firstRequested;

            public bool MoveNext()
            {
                if (firstRequested)
                {
                    current = current?.NextInBucket;
                }
                else
                {
                    firstRequested = true;
                }

                return current is not null;
            }
            
            public readonly KeyValuePair Current => current!;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DefaultSelector(TKey expected) : IEquatable<TKey>, ISupplier<TKey>
    {
        bool IEquatable<TKey>.Equals(TKey? actual) => EqualityComparer<TKey>.Default.Equals(actual, expected);

        TKey ISupplier<TKey>.Invoke() => expected;

        public override int GetHashCode() => EqualityComparer<TKey>.Default.GetHashCode(expected);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DefaultSelectorFactory : ISupplier<TKey, DefaultSelector>
    {
        DefaultSelector ISupplier<TKey, DefaultSelector>.Invoke(TKey key) => new(key);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct EqualityComparerSelector(TKey expected, IEqualityComparer<TKey> comparer) : IEquatable<TKey>, ISupplier<TKey>
    {
        bool IEquatable<TKey>.Equals(TKey? actual) => comparer.Equals(actual, expected);
        
        TKey ISupplier<TKey>.Invoke() => expected;

        public override int GetHashCode() => comparer.GetHashCode(expected);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct EqualityComparerSelectorFactory(IEqualityComparer<TKey> comparer) : ISupplier<TKey, EqualityComparerSelector>
    {
        EqualityComparerSelector ISupplier<TKey, EqualityComparerSelector>.Invoke(TKey key) => new(key, comparer);
    }

    [DebuggerDisplay($"Count = {{{nameof(Count)}}}")]
    internal sealed class BucketList
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private readonly Bucket[] buckets;
        private readonly FastMod fastMod;

        internal BucketList(int length)
        {
            buckets = new Bucket[length];
            fastMod = new((uint)length);
        }

        internal BucketList(BucketList prototype, int length)
            : this(length)
        {
            // import pairs
            foreach (ref var bucket in prototype.buckets.AsSpan())
            {
                foreach (var pair in bucket)
                {
                    // dead pairs are already released
                    if (pair.IsDead)
                        continue;

                    var newPair = pair.Clone();
                    GetByHash(newPair.KeyHashCode).Add(newPair);
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int Count => buckets.Length;

        internal ref Bucket GetByHash(int hashCode)
            => ref this[IndexOf(hashCode)];

        internal int IndexOf(int hashCode)
        {
            var index = fastMod.GetRemainder((uint)hashCode);
            Debug.Assert(index < (uint)buckets.Length);

            return (int)index;
        }

        internal ref Bucket this[int index]
        {
            get
            {
                Debug.Assert(index < (uint)buckets.Length);

                return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), index);
            }
        }

        internal void CleanUp(AsyncParkingLot parkingLot, int hashCode)
        {
            var index = IndexOf(hashCode);
            if (TryAcquire(index))
            {
                try
                {
                    this[index].CleanUp();
                }
                finally
                {
                    Release(parkingLot, index);
                }
            }
        }

        internal KeyValuePair? FindPair<TPredicate>(TPredicate predicate, int keyHashCode)
            where TPredicate : struct, IEquatable<TKey>, allows ref struct
            => GetByHash(keyHashCode).TryGet<TPredicate, NotDeadFilter>(predicate, keyHashCode);

        // The bucket index is the position in the parking lot. The bucket list itself distinguishes
        // the buckets of the different versions with the same index, because the old version can be still in use
        // by the waiters during the resize
        internal ValueTask AcquireAsync(AsyncParkingLot parkingLot, int index, CancellationToken token)
            => parkingLot.ParkAsync(index, ref this[index].LockState, this, token);

        internal ValueTask AcquireAsync(AsyncParkingLot parkingLot, int index, TimeSpan timeout, CancellationToken token)
        {
            var task = parkingLot.ParkAsync(index, ref this[index].LockState, this, timeout, token);
            if (!task.IsCompletedSuccessfully)
                return AcquireSlowAsync(task);

            return task.Result ? ValueTask.CompletedTask : ValueTask.FromException(new TimeoutException());

            static async ValueTask AcquireSlowAsync(ValueTask<bool> task)
            {
                if (!await task.ConfigureAwait(false))
                    throw new TimeoutException();
            }
        }

        private bool TryAcquire(int index)
            => AsyncParkingLot.TryPark(ref this[index].LockState);

        internal void Release(AsyncParkingLot parkingLot, int index)
            => parkingLot.Unpark(index, ref this[index].LockState, this);

        internal void ReleaseFirst(AsyncParkingLot parkingLot, int count)
        {
            Debug.Assert((uint)count <= (uint)buckets.Length);

            for (var i = 0; i < count; i++)
            {
                Release(parkingLot, i);
            }
        }

        internal void Invalidate(Action<KeyValuePair> cleanup)
        {
            foreach (ref var bucket in buckets.AsSpan())
            {
                bucket.Invalidate(cleanup);
            }
        }

        private static IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator(Bucket[] buckets, Action<KeyValuePair> cleanup)
        {
            for (var i = 0; i < buckets.Length; i++)
            {
                for (var current = buckets[i].First; current is not null; current = current.NextInBucket)
                {
                    // SIEVE is not scan-resistant, do not modify eviction state for every read
                    if (current.TryAcquireCounter())
                    {
                        try
                        {
                            yield return new(current.Key, GetValue(current));
                        }
                        finally
                        {
                            if (!current.ReleaseCounter())
                                cleanup(current);
                        }
                    }
                }
            }
        }

        internal IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator(Action<KeyValuePair> cleanup) => GetEnumerator(buckets, cleanup);
    }

    private async ValueTask GrowAsync(BucketList oldVersion, Timeout timeout, CancellationToken token)
    {
        // This is the maximum prime smaller than Array.MaxLength
        const int maxPrimeLength = 0x7FFFFFC3;
        int newSize;
        newSize = oldVersion.Count is maxPrimeLength
            ? throw new InsufficientMemoryException()
            : (uint)(newSize = oldVersion.Count << 1) > maxPrimeLength && maxPrimeLength > oldVersion.Count
                ? maxPrimeLength
                : int.GetPrime(newSize);
            
        // acquire locks
        var lockCount = 0;
        try
        {
            await oldVersion.AcquireAsync(parkingLot, lockCount, timeout.RemainingTime, token).ConfigureAwait(false);
            lockCount++;

            if (!ReferenceEquals(oldVersion, buckets))
                return;

            // acquire the rest of locks
            for (; lockCount < oldVersion.Count; lockCount++)
            {
                await oldVersion.AcquireAsync(parkingLot, lockCount, timeout.RemainingTime, token).ConfigureAwait(false);
            }

            var newSource = new CancelableValueTaskCompletionSource();
            
            // if Exchange returns null then the cache is disposed
            (Interlocked.Exchange(ref completionSource, newSource) ?? newSource).Cancel();
            
            // stop eviction process
            await evictionTask.ConfigureAwait(false);

            // the canceled eviction loop may leave unprocessed promotions, drain them before the rebuild
            ProcessQueue();

            // restart eviction process
            var newVersion = new BucketList(oldVersion, newSize);
            RebuildEvictionState(newVersion);
            queueHead = queueTail = new QueueHead();
            evictionTask = DoEvictionAsync(newSource);

            // Publish the new version last. The locks of the new version are not held,
            // so the writers can promote to the queue as soon as the new version is visible.
            // The waiters for the old version are resumed on release and retry with the new version
            buckets = newVersion;
        }
        finally
        {
            oldVersion.ReleaseFirst(parkingLot, lockCount);
        }
    }
}