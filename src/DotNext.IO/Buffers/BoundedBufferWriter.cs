using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents buffer writer with limited capacity.
/// </summary>
/// <typeparam name="T">The type of the elements in the buffer.</typeparam>
internal sealed class BoundedBufferWriter<T> : IBufferWriter<T>
{
    private readonly ulong maxCapacity;
    private readonly IBufferWriter<T> writer;
    private ulong currentCount;

    public BoundedBufferWriter(IBufferWriter<T> writer, long maxCapacity)
    {
        Debug.Assert(writer is not null);
        Debug.Assert(maxCapacity >= 0L);

        this.maxCapacity = (ulong)maxCapacity;
        this.writer = writer;
    }

    public void Advance(int count)
    {
        writer.Advance(count);
        currentCount += (uint)count;
    }

    public Memory<T> GetMemory(int sizeHint = 0) => GetMemory<MemoryView>(sizeHint);

    public Span<T> GetSpan(int sizeHint = 0) => GetMemory<SpanView>(sizeHint);

    private TMemoryView GetMemory<TMemoryView>(int sizeHint, [CallerArgumentExpression(nameof(sizeHint))] string paramName = "")
        where TMemoryView : IMemoryView<TMemoryView>, allows ref struct
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint, paramName);

        // Check capacity twice, because GetSpan might return the memory block larger than sizeHint
        var newCount = currentCount + (uint)sizeHint;
        if (newCount <= maxCapacity)
        {
            var result = TMemoryView.Create(writer, sizeHint);
            var remainingSpace = maxCapacity - currentCount;
            if (remainingSpace < (uint)result.Length)
                result.Trim(int.CreateTruncating(remainingSpace));

            if (result.Length > 0)
                return result;
        }

        throw new BufferSizeLimitExceededException();
    }

    private interface IMemoryView<out TSelf>
        where TSelf : IMemoryView<TSelf>, allows ref struct
    {
        int Length { get; }

        void Trim(int count);

        public static abstract TSelf Create(IBufferWriter<T> writer, int sizeHint);
    }

    [StructLayout(LayoutKind.Auto)]
    private ref struct SpanView(Span<T> span) : IMemoryView<SpanView>
    {
        private Span<T> span = span;
        
        readonly int IMemoryView<SpanView>.Length => span.Length;

        void IMemoryView<SpanView>.Trim(int count) => span = span.Slice(0, count);

        static SpanView IMemoryView<SpanView>.Create(IBufferWriter<T> writer, int sizeHint)
            => new(writer.GetSpan(sizeHint));

        public static implicit operator Span<T>(SpanView view) => view.span;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct MemoryView(in Memory<T> memory) : IMemoryView<MemoryView>
    {
        private Memory<T> memory = memory;

        readonly int IMemoryView<MemoryView>.Length => memory.Length;

        void IMemoryView<MemoryView>.Trim(int count) => memory = memory.Slice(0, count);

        static MemoryView IMemoryView<MemoryView>.Create(IBufferWriter<T> writer, int sizeHint)
            => new(writer.GetMemory(sizeHint));

        public static implicit operator Memory<T>(MemoryView view) => view.memory;
    }
}