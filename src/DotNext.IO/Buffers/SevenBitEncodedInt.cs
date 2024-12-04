using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

internal struct SevenBitEncodedInt
{
    internal const int MaxSize = 5;

    private uint value;
    private byte shift;

    internal SevenBitEncodedInt(int value) => this.value = (uint)value;

    internal bool Append(byte b)
    {
        if (shift is MaxSize * 7)
            throw new InvalidDataException();

        value |= (b & 0x7FU) << shift;
        shift += 7;
        return (b & 0x80U) is not 0U;
    }

    internal readonly int Value => (int)value;

    public readonly Enumerator GetEnumerator() => new(value);

    internal struct Enumerator
    {
        private uint value;
        private byte current;
        private bool completed;

        internal Enumerator(uint value) => this.value = value;

        public readonly byte Current => current;

        public bool MoveNext()
        {
            if (completed)
                return false;

            if (value > 0x7Fu)
            {
                current = (byte)(value | ~0x7Fu);
                value >>= 7;
            }
            else
            {
                current = (byte)value;
                completed = true;
            }

            return true;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal struct Reader : IBufferReader, ISupplier<int>
    {
        private SevenBitEncodedInt value;
        private bool completed;

        readonly int IBufferReader.RemainingBytes => Unsafe.BitCast<bool, byte>(!completed);

        void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
            => completed = value.Append(MemoryMarshal.GetReference(source)) is false;

        readonly int ISupplier<int>.Invoke() => value.Value;
    }
}