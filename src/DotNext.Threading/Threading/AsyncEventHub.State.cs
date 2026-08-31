using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Collections;

namespace DotNext.Threading;

using Buffers.Binary;
using Collections.Generic;
using Numerics;
using Runtime.CompilerServices;
using Runtime.InteropServices;

partial class AsyncEventHub
{
    /// <summary>
    /// Represents event state as a series of bits.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal struct State : IResettable, IEnumerable<int>
    {
        private UInt128 inlined;
        private BitArray? array;

        public State(int count, bool defaultValue = false)
        {
            Debug.Assert(count > 0);

            array = count > MaxInlinedSize ? new BitArray(count) : null;

            if (!defaultValue)
            {
                // do nothing
            }
            else if (array is null)
            {
                inlined = GetBitMask(count) - UInt128.One;
            }
            else
            {
                array.SetAll(value: true);
            }
        }
        
        private State(in UInt128 state) => inlined = state;

        private State(BitArray state) => array = new(state);

        public readonly void CopyTo(ref State destination)
        {
            if (array is null && destination.array is null)
            {
                RuntimeHelpers.Copy(in inlined, out destination.inlined);
            }
            else
            {
                var bytesWritten = ReadOnlySpan >>> destination.Span;
                destination.Span.Slice(bytesWritten).Clear();
            }
        }

        public readonly int Capacity
        {
            get
            {
                int result;
                if (array is not null)
                {
                    result = array.Length;
                }
                else if (inlined == UInt128.Zero)
                {
                    result = 0;
                }
                else
                {
                    result = MaxInlinedSize - int.CreateTruncating(UInt128.LeadingZeroCount(inlined));
                }

                return result;
            }
        }

        public void Add(int index)
        {
            if (array is not null)
            {
                if (index >= array.Length)
                    array.Length = index + 1;
            }
            else if (index < MaxInlinedSize)
            {
                inlined = inlined.SetBit(index, true);
                return;
            }
            else
            {
                array = new(index + 1);
                MemoryMarshal
                    .AsReadOnlyBytes(in inlined)
                    .CopyTo(CollectionsMarshal.AsBytes(array));
            }

            array[index] = true;
        }

        public readonly int PopCount => array?.PopCount ?? int.CreateTruncating(UInt128.PopCount(inlined));

        public readonly State Clone() => array is null ? new(inlined) : new(array);

        [UnscopedRef]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Span<byte> Span
            => array is null ? MemoryMarshal.AsBytes(ref inlined) : CollectionsMarshal.AsBytes(array);

        [UnscopedRef]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ReadOnlySpan<byte> ReadOnlySpan
            => array is null ? MemoryMarshal.AsReadOnlyBytes(in inlined) : CollectionsMarshal.AsBytes(array);

        public readonly bool IsZeroed
            => array is null ? inlined == UInt128.Zero : ReadOnlySpan.IsZeroed;

        public void SetAll()
        {
            if (array is null)
            {
                inlined = UInt128.MaxValue;
            }
            else
            {
                array.SetAll(value: true);
            }
        }

        public bool Pop(int index)
        {
            bool result;
            if (array is null)
            {
                var mask = GetBitMask(index);
                result = (inlined & mask) == UInt128.Zero;
                inlined = mask;
            }
            else
            {
                result = array[index] is false;
                CollectionsMarshal.AsBytes(array).Clear();
                array[index] = true;
            }

            return result;
        }

        public bool this[int index]
        {
            readonly get => array is null
                ? inlined.IsBitSet(index)
                : array[index];

            set
            {
                if (array is null)
                {
                    inlined = inlined.SetBit(index, value);
                }
                else
                {
                    array[index] = value;
                }
            }
        }

        public void operator &= (in State other)
        {
            if (array is null && other.array is null)
            {
                inlined &= other.inlined;
            }
            else
            {
                AndSlow(Span, other.ReadOnlySpan);
            }

            static void AndSlow(Span<byte> x, ReadOnlySpan<byte> y)
            {
                x = x.TrimLength(y.Length, out var tail);
                if (tail.IsEmpty)
                {
                    x = x.Slice(0, y.Length);
                }
                else
                {
                    tail.Clear();
                }

                Debug.Assert(x.Length == y.Length);
                y.BitwiseAnd(x);
            }
        }

        public void operator |= (in State other)
        {
            if (array is null && other.array is null)
            {
                inlined |= other.inlined;
            }
            else
            {
                OrSlow(Span, other.ReadOnlySpan);
            }
            
            static void OrSlow(Span<byte> x, ReadOnlySpan<byte> y)
            {
                if (x.Length > y.Length)
                {
                    x = x.Slice(0, y.Length);
                }
                else
                {
                    y = y.Slice(0, x.Length);
                }

                y.BitwiseOr(x);
            }
        }

        public void AndNot(in State other)
        {
            if (array is null && other.array is null)
            {
                inlined &= ~other.inlined;
            }
            else
            {
                AndNotSlow(Span, other.ReadOnlySpan);
            }

            static void AndNotSlow(Span<byte> x, ReadOnlySpan<byte> y)
            {
                if (x.Length > y.Length)
                {
                    x = x.Slice(0, y.Length);
                }
                else
                {
                    y = y.Slice(0, x.Length);
                }

                x.AndNot(y);
            }
        }

        public readonly bool CheckMask(in State mask)
        {
            return array is null && mask.array is null
                ? (inlined & mask.inlined) == mask.inlined
                : CheckMaskSlow(ReadOnlySpan, mask.ReadOnlySpan);

            static bool CheckMaskSlow(ReadOnlySpan<byte> value, ReadOnlySpan<byte> mask)
            {
                scoped ReadOnlySpan<byte> tail;
                if (value.Length > mask.Length)
                {
                    tail = [];
                    value = value.Slice(0, mask.Length);
                }
                else
                {
                    tail = mask.Slice(value.Length);
                    mask = mask.Slice(0, value.Length);
                }

                Debug.Assert(mask.Length == value.Length);
                return tail.IsZeroed && value.CheckMask(mask);
            }
        }

        public void Reset()
        {
            if (array is null)
            {
                inlined = UInt128.Zero;
            }
            else
            {
                array.SetAll(value: false);
            }
        }

        private static UInt128 GetBitMask(int index) => UInt128.One << index;

        public readonly Enumerator GetEnumerator() => new(in this);

        private readonly IEnumerator<int> GetClassicEnumerator() => IEnumerator<int>.Create(GetEnumerator());

        readonly IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetClassicEnumerator();

        readonly IEnumerator IEnumerable.GetEnumerator() => GetClassicEnumerator();

        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator(in State state) : IEnumerator<Enumerator, int>
        {
            private UInt128 inlined = state.inlined;
            private BitArrayExtensions.SetBitsEnumerator enumerator = state.array?.SetBits ?? default;

            /// <summary>
            /// Gets the current index.
            /// </summary>
            public int Current
            {
                readonly get;
                private set;
            }

            private bool MoveNextInlined()
            {
                if (inlined == UInt128.Zero)
                    return false;

                var index = int.CreateTruncating(UInt128.TrailingZeroCount(inlined));
                Current = index;
                inlined ^= GetBitMask(index);
                return true;
            }

            /// <inheritdoc cref="IEnumerator.MoveNext()"/>
            public bool MoveNext()
            {
                bool result;
                if (enumerator.IsDefault)
                {
                    result = MoveNextInlined();
                }
                else if (enumerator.MoveNext())
                {
                    result = true;
                    Current = enumerator.Current;
                }
                else
                {
                    result = false;
                }

                return result;
            }
        }
    }
}