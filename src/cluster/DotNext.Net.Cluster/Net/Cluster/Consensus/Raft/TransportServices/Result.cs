using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using Buffers.Binary;
using static Runtime.Intrinsics;

internal readonly struct Result : IBinaryFormattable<Result>
{
    internal const int Size = sizeof(long) + sizeof(byte);

    private readonly Result<byte> value;

    private Result(long term, byte value) => this.value = new() { Term = term, Value = value };

    static int IBinaryFormattable<Result>.Size => Size;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref readonly Result AsFormattable<T>(in Result<T> result)
        where T : unmanaged
    {
        Debug.Assert(AreCompatible<T, byte>());

        return ref Unsafe.As<Result<T>, Result>(ref Unsafe.AsRef(in result));
    }

    internal static ref readonly Result AsFormattable(in Result<bool> result)
        => ref AsFormattable<bool>(in result);

    internal static ref readonly Result AsFormattable(in Result<PreVoteResult> result)
        => ref AsFormattable<PreVoteResult>(in result);

    internal static ref readonly Result AsFormattable(in Result<HeartbeatResult> result)
        => ref AsFormattable<HeartbeatResult>(in result);

    public void Format(Span<byte> destination)
    {
        var writer = new SpanWriter<byte>(destination);
        writer.WriteLittleEndian(value.Term);
        writer.Add() = value.Value;
    }

    public static Result Parse(ReadOnlySpan<byte> source)
    {
        var reader = new SpanReader<byte>(source);
        return new(reader.ReadLittleEndian<long>(), reader.Read());
    }

    public static implicit operator Result<bool>(Result value)
        => Unsafe.BitCast<Result, Result<bool>>(value);

    public static implicit operator Result<PreVoteResult>(Result value)
        => Unsafe.BitCast<Result, Result<PreVoteResult>>(value);

    public static implicit operator Result<HeartbeatResult>(Result value)
        => Unsafe.BitCast<Result, Result<HeartbeatResult>>(value);
}