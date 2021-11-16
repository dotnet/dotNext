using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext.Buffers;

public partial struct BufferWriterSlim<T>
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly ref struct Ref
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ReadOnlySpan<byte> reference;

        internal Ref(ref BufferWriterSlim<T> writer)
        {
            reference = MemoryMarshal.CreateSpan(ref AsRef(ref writer), Size);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static ref byte AsRef(ref BufferWriterSlim<T> writer)
            {
                Ldarg(nameof(writer));
                return ref ReturnRef<byte>();
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal ref BufferWriterSlim<T> Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref AsWriter(ref MemoryMarshal.GetReference(reference));

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static ref BufferWriterSlim<T> AsWriter(ref byte reference)
                {
                    Push(ref reference);
                    Ret();
                    throw Unreachable();
                }
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static int Size
    {
        get
        {
            Sizeof(typeof(BufferWriterSlim<T>));
            return Return<int>();
        }
    }
}