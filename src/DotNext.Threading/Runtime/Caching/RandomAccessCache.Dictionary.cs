using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

using Numerics;
using Threading;

public partial class RandomAccessCache<TKey, TValue>
{
    // devirtualize Value getter manually (JIT will replace this method with one of the actual branches)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TValue GetValue(KeyValuePair pair)
    {
        Debug.Assert(pair is not FakeKeyValuePair);
        Debug.Assert(Atomic.IsAtomic<TValue>() ? pair is KeyValuePairAtomicAccess : pair is KeyValuePairNonAtomicAccess);

        return Atomic.IsAtomic<TValue>()
            ? Unsafe.As<KeyValuePairAtomicAccess>(pair).Value
            : Unsafe.As<KeyValuePairNonAtomicAccess>(pair).Value;
    }

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

        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
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
            ? new KeyValuePairAtomicAccess(key, value, hashCode)
            : new KeyValuePairNonAtomicAccess(key, value, hashCode);
    }

    private readonly Bucket[] buckets;
    private readonly ulong fastModMultiplier;

    private Bucket GetBucket(int hashCode)
    {
        var index = (int)(IntPtr.Size is sizeof(ulong)
            ? PrimeNumber.FastMod((uint)hashCode, (uint)buckets.Length, fastModMultiplier)
            : (uint)hashCode % (uint)buckets.Length);

        Debug.Assert((uint)index < (uint)buckets.Length);

        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), index);
    }

    // the caller is responsible to clean up value stored in the pair
    private KeyValuePair? TryRemove(IEqualityComparer<TKey>? keyComparer, Bucket bucket, TKey key, int hashCode)
    {
        var result = default(KeyValuePair?);

        // remove all dead nodes from the bucket
        if (keyComparer is null)
        {
            for (KeyValuePair? current = bucket.First, previous = null;
                 current is not null;
                 previous = current, current = current.NextInBucket)
            {
                if (result is null && hashCode == current.KeyHashCode &&
                    (typeof(TKey).IsValueType ? EqualityComparer<TKey>.Default.Equals(key, current.Key) : current.Key.Equals(key)))
                {
                    Remove(bucket, previous, current);
                    result = current.TryMarkAsEvicted() ? current : null;
                }

                if (current.IsDead)
                {
                    Remove(bucket, previous, current);
                }
            }
        }
        else
        {
            for (KeyValuePair? current = bucket.First, previous = null;
                 current is not null;
                 previous = current, current = current.NextInBucket)
            {
                if (result is null && hashCode == current.KeyHashCode && keyComparer.Equals(key, current.Key))
                {
                    Remove(bucket, previous, current);
                    result = current.TryMarkAsEvicted() ? current : null;
                }

                if (current.IsDead)
                {
                    Remove(bucket, previous, current);
                }
            }
        }

        return result;
    }

    private static void Remove(Bucket bucket, KeyValuePair? previous, KeyValuePair current)
    {
        ref var location = ref previous is null ? ref bucket.First : ref previous.NextInBucket;
        Volatile.Write(ref location, current.NextInBucket);
    }

    private CacheEntryHandle Modify(IEqualityComparer<TKey>? keyComparer, Bucket bucket, TKey key, int hashCode)
    {
        KeyValuePair? previous = null, valueHolder = null;
        if (keyComparer is null)
        {
            for (var current = bucket.First; current is not null; previous = current, current = current.NextInBucket)
            {
                if (valueHolder is null && hashCode == current.KeyHashCode &&
                    (typeof(TKey).IsValueType ? EqualityComparer<TKey>.Default.Equals(key, current.Key) : current.Key.Equals(key)) &&
                    current.Visit() && current.TryAcquireCounter())
                {
                    valueHolder = current;
                }

                if (current.IsDead)
                {
                    Remove(bucket, previous, current);
                }
            }
        }
        else
        {
            for (var current = bucket.First; current is not null; previous = current, current = current.NextInBucket)
            {
                if (valueHolder is null && hashCode == current.KeyHashCode && keyComparer.Equals(key, current.Key) && current.Visit() &&
                    current.TryAcquireCounter())
                {
                    valueHolder = current;
                }

                if (current.IsDead)
                {
                    Remove(bucket, previous, current);
                }
            }
        }

        return valueHolder is null
            ? new(this, bucket, previous, key, hashCode)
            : new(this, valueHolder);
    }

    private KeyValuePair? TryGet(IEqualityComparer<TKey>? keyComparer, Bucket bucket, TKey key, int hashCode)
    {
        var result = default(KeyValuePair?);

        // remove all dead nodes from the bucket
        if (keyComparer is null)
        {
            for (KeyValuePair? current = bucket.First, previous = null;
                 current is not null;
                 previous = current, current = current.NextInBucket)
            {
                if (result is null && hashCode == current.KeyHashCode &&
                    (typeof(TKey).IsValueType ? EqualityComparer<TKey>.Default.Equals(key, current.Key) : current.Key.Equals(key)) &&
                    current.Visit() && current.TryAcquireCounter())
                {
                    result = current;
                }

                if (current.IsDead)
                {
                    Remove(bucket, previous, current);
                }
            }
        }
        else
        {
            for (KeyValuePair? current = bucket.First, previous = null;
                 current is not null;
                 previous = current, current = current.NextInBucket)
            {
                if (result is null && hashCode == current.KeyHashCode && keyComparer.Equals(key, current.Key) && current.Visit() &&
                    current.TryAcquireCounter())
                {
                    result = current;
                }

                if (current.IsDead)
                {
                    Remove(bucket, previous, current);
                }
            }
        }

        return result;
    }

    internal partial class KeyValuePair(TKey key, int hashCode) : TaskCompletionSource(TaskCreationOptions.None)
    {
        internal readonly int KeyHashCode = hashCode;
        internal readonly TKey Key = key;
        internal volatile KeyValuePair? NextInBucket; // volatile, used by the dictionary subsystem only
        private int lifetimeCounter = 1;

        internal bool TryAcquireCounter()
        {
            int currentValue, newValue, tmp = Volatile.Read(in lifetimeCounter);
            do
            {
                currentValue = tmp;
                if (currentValue is 0)
                    break;

                newValue = currentValue + 1;
            } while ((tmp = Interlocked.CompareExchange(ref lifetimeCounter, newValue, currentValue)) != currentValue);

            return currentValue > 0U;
        }

        internal bool ReleaseCounter() => Interlocked.Decrement(ref lifetimeCounter) > 0;
    }

    private sealed class KeyValuePairAtomicAccess : KeyValuePair
    {
        internal TValue Value;

        internal KeyValuePairAtomicAccess(TKey key, TValue value, int hashCode)
            : base(key, hashCode)
            => Value = value;

        public override string ToString() => $"Key = {Key} Value = {Value}";
    }

    // non-atomic access utilizes copy-on-write semantics
    private sealed class KeyValuePairNonAtomicAccess : KeyValuePair
    {
        private sealed class ValueHolder
        {
            internal readonly TValue Value;

            internal ValueHolder(TValue value) => Value = value;
        }

        private static readonly ValueHolder DefaultHolder = new(default!);
        private ValueHolder holder;

        internal KeyValuePairNonAtomicAccess(TKey key, TValue value, int hashCode)
            : base(key, hashCode)
            => holder = new(value);

        internal TValue Value
        {
            get => holder.Value;
            set => holder = new(value);
        }

        internal void ClearValue() => holder = DefaultHolder;

        public override string ToString() => $"Key = {Key} Value = {Value}";
    }
    
    internal sealed class Bucket : AsyncExclusiveLock
    {
        internal volatile KeyValuePair? First; // volatile
    }
}