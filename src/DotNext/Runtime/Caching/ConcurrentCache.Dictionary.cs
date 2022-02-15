using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

using Threading;

public partial class ConcurrentCache<TKey, TValue>
{
    private static readonly bool IsValueWriteAtomic;

    [DebuggerDisplay($"Key = {{{nameof(Key)}}} Value = {{{nameof(Value)}}}")]
    private abstract class KeyValuePair
    {
        // index = eviction deque
        private readonly (KeyValuePair? Previous, KeyValuePair? Next)[] links;
        internal readonly int KeyHashCode;
        internal readonly TKey Key;
        internal volatile KeyValuePair? Next;
        internal AtomicBoolean IsAlive;

        private protected KeyValuePair(TKey key, int hashCode, int buffersCount)
        {
            Key = key;
            KeyHashCode = hashCode;
            IsAlive = new(true);
            links = new (KeyValuePair? Previous, KeyValuePair? Next)[buffersCount];
        }

        internal abstract TValue Value { get; set; }

        internal ref (KeyValuePair? Previous, KeyValuePair? Next) GetLinks(int dequeIndex)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(links), dequeIndex);
    }

    private sealed class KeyValuePairAtomicAccess : KeyValuePair
    {
        internal KeyValuePairAtomicAccess(TKey key, TValue value, int hashCode, int buffersCount)
            : base(key, hashCode, buffersCount)
            => Value = value;

        internal override TValue Value { get; set; }
    }

    // non-atomic access utilizes copy-on-write semantics
    private sealed class KeyValuePairNonAtomicAccess : KeyValuePair
    {
        private object value;

        internal KeyValuePairNonAtomicAccess(TKey key, TValue value, int hashCode, int buffersCount)
            : base(key, hashCode, buffersCount)
            => this.value = value!;

        internal override TValue Value
        {
            get => (TValue)value;
            set => this.value = value!;
        }
    }

    private sealed class SentinelKeyValuePair : KeyValuePair
    {
        internal SentinelKeyValuePair(int buffersCount)
            : base(default!, 0, buffersCount)
        {
        }

        internal override TValue Value
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    private sealed class Table
    {
        private readonly KeyValuePair?[] buckets;
        private readonly object[] locks;
        internal readonly IEqualityComparer<TKey>? KeyComparer;
        private int count; // volatile

        internal Table(int capacity, IEqualityComparer<TKey>? keyComparer)
        {
            buckets = new KeyValuePair?[capacity];
            Span.Initialize<object>(locks = new object[capacity]);
            KeyComparer = keyComparer;
        }

        internal int Capacity => buckets.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref KeyValuePair? GetBucket(int hashCode)
        {
            var index = (uint)hashCode % (uint)buckets.Length;

            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), index);
        }

        internal ref KeyValuePair? GetBucket(int hashCode, out object bucketLock)
        {
            var index = (uint)hashCode % (uint)buckets.Length;

            bucketLock = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(locks), index);
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), index);
        }

        internal void Remove(KeyValuePair expected)
        {
            ref var bucket = ref GetBucket(expected.KeyHashCode, out var bucketLock);
            lock (bucketLock)
            {
                for (KeyValuePair? actual = Volatile.Read(ref bucket), previous = null; actual is not null; previous = actual, actual = actual.Next)
                {
                    if (ReferenceEquals(expected, actual))
                    {
                        if (previous is null)
                            Volatile.Write(ref bucket, actual.Next);
                        else
                            previous.Next = actual.Next;

                        OnRemoved();
                    }
                }
            }
        }

        internal int AcquireAllLocks()
        {
            int i;
            for (i = 0; i < locks.Length; i++)
                Monitor.Enter(locks[i]);

            return i;
        }

        internal void ReleaseLocks(int acquiredLocks)
        {
            for (var i = 0; i < acquiredLocks; i++)
                Monitor.Exit(locks[i]);
        }

        internal void Clear()
        {
            foreach (ref var root in buckets.AsSpan())
            {
                for (var current = Volatile.Read(ref root); current is not null; current = current.Next)
                    current.IsAlive.Value = false;

                Volatile.Write(ref root, null);
            }
        }

        internal IEnumerable<TKey> Keys
        {
            get
            {
                for (var i = 0; i < buckets.Length; i++)
                {
                    for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                    {
                        var key = current.Key;
                        if (current.IsAlive.Value)
                            yield return key;
                    }
                }
            }
        }

        internal IEnumerable<TValue> Values
        {
            get
            {
                for (var i = 0; i < buckets.Length; i++)
                {
                    for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                    {
                        var value = current.Value;
                        if (current.IsAlive.Value)
                            yield return value;
                    }
                }
            }
        }

        internal IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (var i = 0; i < buckets.Length; i++)
            {
                for (var current = Volatile.Read(ref buckets[i]); current is not null; current = current.Next)
                {
                    var pair = new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                    if (current.IsAlive.Value)
                        yield return pair;
                }
            }
        }

        internal int Count => Volatile.Read(ref count);

        internal void OnAdded() => Interlocked.Increment(ref count);

        internal void OnRemoved() => Interlocked.Decrement(ref count);
    }

    private readonly Table table;

    private bool TryAdd(TKey key, TValue value, bool updateIfExists, out TValue? previous)
    {
        var keyComparer = table.KeyComparer;
        var hashCode = keyComparer?.GetHashCode(key) ?? key.GetHashCode();
        ref var bucket = ref table.GetBucket(hashCode, out var bucketLock);
        bool result;
        CommandType command;
        KeyValuePair pair;

        lock (bucketLock)
        {
            if (keyComparer is null)
            {
                for (KeyValuePair? current = Volatile.Read(ref bucket); current is not null; current = current.Next)
                {
                    if (!current.IsAlive.Value)
                        break;

                    if (hashCode == current.KeyHashCode && (typeof(TKey).IsValueType ? EqualityComparer<TKey>.Default.Equals(key, current.Key) : current.Key.Equals(key)))
                    {
                        previous = current.Value;
                        result = false;
                        if (updateIfExists)
                        {
                            current.Value = value;
                            command = CommandType.Read;
                            pair = current;
                            goto enqueue_and_exit;
                        }

                        goto exit;
                    }
                }
            }
            else
            {
                for (KeyValuePair? current = Volatile.Read(ref bucket); current is not null; current = current.Next)
                {
                    if (!current.IsAlive.Value)
                        break;

                    if (hashCode == current.KeyHashCode && keyComparer.Equals(key, current.Key))
                    {
                        previous = current.Value;
                        result = false;
                        if (updateIfExists)
                        {
                            current.Value = value;
                            command = CommandType.Read;
                            pair = current;
                            goto enqueue_and_exit;
                        }

                        goto exit;
                    }
                }
            }

            previous = default;
            pair = IsValueWriteAtomic
                ? new KeyValuePairAtomicAccess(key, value, hashCode, concurrencyLevel)
                : new KeyValuePairNonAtomicAccess(key, value, hashCode, concurrencyLevel);
            pair.Next = bucket;
            Volatile.Write(ref bucket, pair);
            command = CommandType.Add;
            result = true;
            table.OnAdded();
        }

    enqueue_and_exit:
        EnqueueCommandAndDrainQueue(command, pair);

    exit:
        return result;
    }
}