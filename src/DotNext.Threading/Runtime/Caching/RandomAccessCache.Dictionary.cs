using System.ComponentModel;
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
        Debug.Assert(pair is not FakeKeyValuePair);
        Debug.Assert(Atomic.IsAtomic<TValue>() ? pair is KeyValuePairAtomicAccess : pair is KeyValuePairNonAtomicAccess);

        return ref Atomic.IsAtomic<TValue>()
            ? ref Unsafe.As<KeyValuePairAtomicAccess>(pair).Value
            : ref Unsafe.As<KeyValuePairNonAtomicAccess>(pair).ValueRef;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetValue(KeyValuePair pair, TValue value)
    {
        Debug.Assert(pair is not FakeKeyValuePair);

        if (Atomic.IsAtomic<TValue>())
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
        Debug.Assert(pair is not FakeKeyValuePair);

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
        {
            // do nothing
        }
        else if (Atomic.IsAtomic<TValue>())
        {
            Unsafe.As<KeyValuePairAtomicAccess>(pair).Value = default!;
        }
        else
        {
            Unsafe.As<KeyValuePairNonAtomicAccess>(pair).ClearValue();
        }
    }

    private static KeyValuePair CreatePair(TKey key, TValue value, int hashCode)
    {
        return Atomic.IsAtomic<TValue>()
            ? new KeyValuePairAtomicAccess(key, hashCode) { Value = value }
            : new KeyValuePairNonAtomicAccess(key, hashCode) { Value = value };
    }

    private volatile BucketList buckets;

    internal partial class KeyValuePair(TKey key, int hashCode)
    {
        internal readonly int KeyHashCode = hashCode;
        internal readonly TKey Key = key;
        internal volatile KeyValuePair? NextInBucket; // volatile, used by the dictionary subsystem only
        
        // Reference counting is used to establish lifetime of the stored value (not KeyValuePair instance).
        // Initial value 1 means that the pair is referenced by the eviction queue. There
        // are two competing threads that may decrement the counter to zero: removal thread (see TryRemove)
        // and eviction thread. To synchronize the decision, 'cacheState' is used. The thread that evicts the pair
        // successfully (transition from 0 => -1) is able to decrement the counter to zero.
        private volatile int lifetimeCounter = 1;

        internal void Import(KeyValuePair other)
        {
            cacheState = other.cacheState;
            notification = other.notification;
            addedEvent = other.addedEvent;
        }

        internal bool TryAcquireCounter()
        {
            int currentValue, tmp = lifetimeCounter;
            do
            {
                currentValue = tmp;
                if (currentValue is 0)
                    break;
            } while ((tmp = Interlocked.CompareExchange(ref lifetimeCounter, currentValue + 1, currentValue)) != currentValue);

            return currentValue > 0U;
        }

        internal bool ReleaseCounter() => Interlocked.Decrement(ref lifetimeCounter) > 0;
        
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

    private sealed class KeyValuePairAtomicAccess(TKey key, int hashCode) : KeyValuePair(key, hashCode)
    {
        internal required TValue Value;

        public override string ToString() => ToString(Value);
    }

    // non-atomic access utilizes copy-on-write semantics
    private sealed class KeyValuePairNonAtomicAccess(TKey key, int hashCode) : KeyValuePair(key, hashCode)
    {
        private sealed class ValueHolder(TValue value)
        {
            internal readonly TValue Value = value;
        }

        private static readonly ValueHolder DefaultHolder = new(default!);
        
        private ValueHolder holder;

        internal required TValue Value
        {
            get => holder.Value;

            [MemberNotNull(nameof(holder))] set => holder = new(value);
        }

        internal ref readonly TValue ValueRef => ref holder.Value;

        internal void ClearValue() => holder = DefaultHolder;

        public override string ToString() => ToString(Value);
    }
    
    internal interface IKeyValuePairVisitor
    {
        public static abstract bool Visit(KeyValuePair pair);
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct NotDeadFilter : IKeyValuePairVisitor
    {
        static bool IKeyValuePairVisitor.Visit(KeyValuePair pair) => !pair.IsDead;
    }

    [DebuggerDisplay($"NumberOfItems = {{{nameof(Count)}}}, IsLockHeld = {{{nameof(IsLockHeld)}}}")]
    [StructLayout(LayoutKind.Auto)]
    internal struct Bucket(AsyncExclusiveLock bucketLock)
    {
        internal readonly AsyncExclusiveLock Lock = bucketLock;
        private KeyValuePair? addedPair;
        private volatile KeyValuePair? first;
        private volatile int count;

        public Bucket()
            : this(new())
        {
        }
        
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal readonly int CollisionCount => count;

        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly (int Alive, int Dead) Count => first?.BucketNodesCount ?? default;

        [ExcludeFromCodeCoverage]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly bool? IsLockHeld => Lock?.IsLockHeld;

        internal KeyValuePair? TryAdd(TKey key, int hashCode, TValue value)
        {
            KeyValuePair? result;
            if (addedPair is not null)
            {
                result = null;
            }
            else
            {
                Add(addedPair = result = CreatePair(key, value, hashCode));
            }

            return result;
        }

        internal void Add(KeyValuePair pair)
        {
            // possible contention with TryRemove private method that can be called without lock
            KeyValuePair? current, tmp = first;
            do
            {
                pair.NextInBucket = current = tmp;
            } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref first, pair, current), current));

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

        internal KeyValuePair? TryRemove(IEqualityComparer<TKey>? keyComparer, TKey key, int hashCode)
        {
            var result = default(KeyValuePair?);

            // remove all dead nodes from the bucket
            if (keyComparer is null)
            {
                for (KeyValuePair? current = first, previous = null;
                     current is not null;
                     previous = current, current = current.NextInBucket)
                {
                    if (result is null && hashCode == current.KeyHashCode
                                       && EqualityComparer<TKey>.Default.Equals(key, current.Key)
                                       && current.MarkAsEvicted())
                    {
                        result = current;
                    }

                    if (current.IsDead)
                    {
                        TryRemove(previous, current);
                    }
                }
            }
            else
            {
                for (KeyValuePair? current = first, previous = null;
                     current is not null;
                     previous = current, current = current.NextInBucket)
                {
                    if (result is null && hashCode == current.KeyHashCode
                                       && keyComparer.Equals(key, current.Key)
                                       && current.MarkAsEvicted())
                    {
                        result = current;
                    }

                    if (current.IsDead)
                    {
                        TryRemove(previous, current);
                    }
                }
            }

            return result;
        }

        internal KeyValuePair? TryGet<TVisitor>(IEqualityComparer<TKey>? keyComparer, TKey key, int hashCode)
            where TVisitor : struct, IKeyValuePairVisitor
        {
            var result = default(KeyValuePair?);

            // remove all dead nodes from the bucket
            if (keyComparer is null)
            {
                for (KeyValuePair? current = first, previous = null;
                     current is not null;
                     previous = current, current = current.NextInBucket)
                {
                    if (result is null && hashCode == current.KeyHashCode
                                       && EqualityComparer<TKey>.Default.Equals(key, current.Key)
                                       && TVisitor.Visit(current))
                    {
                        result = current;
                    }

                    if (current.IsDead)
                    {
                        TryRemove(previous, current);
                    }
                }
            }
            else
            {
                for (KeyValuePair? current = first, previous = null;
                     current is not null;
                     previous = current, current = current.NextInBucket)
                {
                    if (result is null && hashCode == current.KeyHashCode
                                       && keyComparer.Equals(key, current.Key)
                                       && TVisitor.Visit(current))
                    {
                        result = current;
                    }

                    if (current.IsDead)
                    {
                        TryRemove(previous, current);
                    }
                }
            }

            return result;
        }

        internal KeyValuePair? Modify(IEqualityComparer<TKey>? keyComparer, TKey key, int hashCode)
        {
            KeyValuePair? valueHolder = null;
            if (keyComparer is null)
            {
                for (KeyValuePair? current = first, previous = null; current is not null; previous = current, current = current.NextInBucket)
                {
                    if (valueHolder is null && hashCode == current.KeyHashCode
                                            && EqualityComparer<TKey>.Default.Equals(key, current.Key)
                                            && current.Visit()
                                            && current.TryAcquireCounter())
                    {
                        valueHolder = current;
                    }

                    if (current.IsDead)
                    {
                        TryRemove(previous, current);
                    }
                }
            }
            else
            {
                for (KeyValuePair? current = first, previous = null; current is not null; previous = current, current = current.NextInBucket)
                {
                    if (valueHolder is null && hashCode == current.KeyHashCode
                                            && keyComparer.Equals(key, current.Key)
                                            && current.Visit()
                                            && current.TryAcquireCounter())
                    {
                        valueHolder = current;
                    }

                    if (current.IsDead)
                    {
                        TryRemove(previous, current);
                    }
                }
            }

            return valueHolder;
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
        
        [StructLayout(LayoutKind.Auto)]
        internal readonly struct Ref(Bucket[] buckets, int index)
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            internal ref Bucket Value => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), index);
        }
    }
    
    [DebuggerDisplay($"Count = {{{nameof(Count)}}}")]
    private sealed class BucketList
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private readonly Bucket[] buckets;
        private readonly FastMod fastMod;

        internal BucketList(int length)
        {
            Span.Initialize<Bucket>(buckets = new Bucket[length]);
            fastMod = new((uint)length);
        }

        internal BucketList(BucketList prototype, int length)
        {
            buckets = new Bucket[length];
            fastMod = new((uint)length);

            // import pairs
            Span<Bucket> prototypeBuckets = prototype.buckets;
            int i;
            for (i = 0; i < prototypeBuckets.Length; i++)
            {
                buckets[i] = new(prototypeBuckets[i].Lock);
            }

            buckets.AsSpan(i).Initialize();
            
            foreach (ref var bucket in prototypeBuckets)
            {
                foreach (var pair in bucket)
                {
                    var newPair = CreatePair(pair.Key, GetValue(pair), pair.KeyHashCode);
                    newPair.Import(pair);
                    GetByHash(newPair.KeyHashCode).Add(newPair);
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int Count => buckets.Length;

        internal void GetByHash(int hashCode, out Bucket.Ref bucket)
        {
            var index = fastMod.GetRemainder((uint)hashCode);
            Debug.Assert(index < (uint)buckets.Length);

            bucket = new(buckets, (int)index);
        }

        internal ref Bucket GetByHash(int hashCode)
            => ref GetByIndex((int)fastMod.GetRemainder((uint)hashCode));

        internal ref Bucket GetByIndex(int index)
        {
            Debug.Assert(index < (uint)buckets.Length);
            
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), index);
        }

        internal KeyValuePair? FindPair(IEqualityComparer<TKey>? keyComparer, TKey key, int keyHashCode)
            => GetByHash(keyHashCode).TryGet<NotDeadFilter>(keyComparer, key, keyHashCode);

        internal void Release(int count)
        {
            Debug.Assert((uint)count <= (uint)buckets.Length);

            for (var i = 0; i < count; i++)
            {
                buckets[i].Lock.Release();
            }
        }

        internal void Invalidate(Action<KeyValuePair> cleanup)
        {
            foreach (ref var bucket in buckets.AsSpan())
            {
                bucket.Invalidate(cleanup);
            }
        }
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
                : PrimeNumber.GetPrime(newSize);
            
        // acquire locks
        var lockCount = 0;
        try
        {
            var bucketLock = oldVersion.GetByIndex(lockCount).Lock;
            await bucketLock.AcquireAsync(timeout.GetRemainingTimeOrZero(), token).ConfigureAwait(false);
            lockCount++;

            if (!ReferenceEquals(oldVersion, buckets))
                return;

            // acquire the rest of locks
            for (; lockCount < oldVersion.Count; lockCount++)
            {
                bucketLock = oldVersion.GetByIndex(lockCount).Lock;
                await bucketLock.AcquireAsync(timeout.GetRemainingTimeOrZero(), token).ConfigureAwait(false);
            }
            
            var newSource = new CancelableValueTaskCompletionSource();
            
            // if Exchange returns null then the cache is disposed
            (Interlocked.Exchange(ref completionSource, newSource) ?? newSource).Cancel();
            
            // stop eviction process
            await evictionTask.ConfigureAwait(false);

            // restart eviction process
            RebuildEvictionState(buckets = new(oldVersion, newSize));
            queueHead = queueTail = new FakeKeyValuePair();
            evictionTask = DoEvictionAsync(newSource);
        }
        finally
        {
            oldVersion.Release(lockCount);
        }
    }
}