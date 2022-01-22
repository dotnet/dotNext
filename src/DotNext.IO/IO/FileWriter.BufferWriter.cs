using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

public partial class FileWriter : IBufferWriter<byte>
{
    /// <inheritdoc />
    void IBufferWriter<byte>.Advance(int count) => Produce(count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void VerifyBufferSize(ref int sizeHint)
    {
        switch (sizeHint)
        {
            case < 0:
                Throw();
                break;
            case 0:
                sizeHint = 1;
                break;
        }

        [DoesNotReturn]
        [StackTraceHidden]
        static void Throw() => throw new ArgumentOutOfRangeException(nameof(sizeHint));
    }

    /// <inheritdoc />
    Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
    {
        VerifyBufferSize(ref sizeHint);

        var result = Buffer;
        return sizeHint <= result.Length ? result : throw new InsufficientMemoryException();
    }

    /// <inheritdoc />
    Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
    {
        VerifyBufferSize(ref sizeHint);

        var result = BufferSpan;
        return sizeHint <= result.Length ? result : throw new InsufficientMemoryException();
    }
}