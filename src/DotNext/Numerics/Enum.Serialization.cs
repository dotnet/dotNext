using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Numerics;

partial struct Enum<T>
{
    /// <inheritdoc/>
    public int GetByteCount()
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value),
            TypeCode.SByte => ConstrainedCall<sbyte>(value),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value),
            TypeCode.Int16 => ConstrainedCall<short>(value),
            TypeCode.UInt32 => ConstrainedCall<uint>(value),
            TypeCode.Int32 => ConstrainedCall<int>(value),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value),
            TypeCode.Int64 => ConstrainedCall<long>(value),
            _ => Unsafe.SizeOf<T>(),
        };
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int ConstrainedCall<TValue>(T value)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<T, TValue>(value).GetByteCount();
        }
    }

    /// <inheritdoc/>
    public static Enum<T> TrailingZeroCount(Enum<T> value)
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value),
            TypeCode.SByte => ConstrainedCall<sbyte>(value),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value),
            TypeCode.Int16 => ConstrainedCall<short>(value),
            TypeCode.UInt32 => ConstrainedCall<uint>(value),
            TypeCode.Int32 => ConstrainedCall<int>(value),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value),
            TypeCode.Int64 => ConstrainedCall<long>(value),
            _ => default,
        };
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Enum<T> ConstrainedCall<TValue>(Enum<T> value)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, Enum<T>>(
                TValue.TrailingZeroCount(Unsafe.BitCast<Enum<T>, TValue>(value)));
        }
    }

    /// <inheritdoc/>
    public static bool TryReadBigEndian(ReadOnlySpan<byte> source, bool isUnsigned, out Enum<T> value)
        => TryRead<BigEndianness>(source, isUnsigned, out value);

    /// <inheritdoc/>
    public static bool TryReadLittleEndian(ReadOnlySpan<byte> source, bool isUnsigned, out Enum<T> value)
        => TryRead<LittleEndianness>(source, isUnsigned, out value);

    private static bool TryRead<TSerializer>(ReadOnlySpan<byte> source, bool isUnsigned, out Enum<T> result)
        where TSerializer : EnumHelpers.ISerializer, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(source, isUnsigned, out result),
            TypeCode.SByte => ConstrainedCall<sbyte>(source, isUnsigned, out result),
            TypeCode.UInt16 => ConstrainedCall<ushort>(source, isUnsigned, out result),
            TypeCode.Int16 => ConstrainedCall<short>(source, isUnsigned, out result),
            TypeCode.UInt32 => ConstrainedCall<uint>(source, isUnsigned, out result),
            TypeCode.Int32 => ConstrainedCall<int>(source, isUnsigned, out result),
            TypeCode.UInt64 => ConstrainedCall<ulong>(source, isUnsigned, out result),
            TypeCode.Int64 => ConstrainedCall<long>(source, isUnsigned, out result),
            _ => EnumHelpers.Fail(out result),
        };
        
        static bool ConstrainedCall<TValue>(ReadOnlySpan<byte> source, bool isUnsigned, out Enum<T> result)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            Unsafe.SkipInit(out result);
            return TSerializer.TryRead(source, isUnsigned, out Unsafe.As<Enum<T>, TValue>(ref result));
        }
    }

    /// <inheritdoc/>
    public static Enum<T> ReadLittleEndian(ReadOnlySpan<byte> source, bool isUnsigned)
        => Read<LittleEndianness>(source, isUnsigned);

    /// <inheritdoc/>
    public static Enum<T> ReadBigEndian(ReadOnlySpan<byte> source, bool isUnsigned)
        => Read<BigEndianness>(source, isUnsigned);

    private static Enum<T> Read<TSerializer>(ReadOnlySpan<byte> source, bool isUnsigned)
        where TSerializer : EnumHelpers.ISerializer, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(source, isUnsigned),
            TypeCode.SByte => ConstrainedCall<sbyte>(source, isUnsigned),
            TypeCode.UInt16 => ConstrainedCall<ushort>(source, isUnsigned),
            TypeCode.Int16 => ConstrainedCall<short>(source, isUnsigned),
            TypeCode.UInt32 => ConstrainedCall<uint>(source, isUnsigned),
            TypeCode.Int32 => ConstrainedCall<int>(source, isUnsigned),
            TypeCode.UInt64 => ConstrainedCall<ulong>(source, isUnsigned),
            TypeCode.Int64 => ConstrainedCall<long>(source, isUnsigned),
            _ => default,
        };
        
        static Enum<T> ConstrainedCall<TValue>(ReadOnlySpan<byte> source, bool isUnsigned)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, Enum<T>>(TSerializer.Read<TValue>(source, isUnsigned));
        }
    }

    /// <inheritdoc/>
    public bool TryWriteBigEndian(Span<byte> destination, out int bytesWritten)
        => TryWrite<BigEndianness>(destination, out bytesWritten);

    /// <inheritdoc/>
    public bool TryWriteLittleEndian(Span<byte> destination, out int bytesWritten)
        => TryWrite<LittleEndianness>(destination, out bytesWritten);

    private bool TryWrite<TSerializer>(Span<byte> destination, out int bytesWritten)
        where TSerializer : EnumHelpers.ISerializer, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value, destination, out bytesWritten),
            TypeCode.SByte => ConstrainedCall<sbyte>(value, destination, out bytesWritten),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value, destination, out bytesWritten),
            TypeCode.Int16 => ConstrainedCall<short>(value, destination, out bytesWritten),
            TypeCode.UInt32 => ConstrainedCall<uint>(value, destination, out bytesWritten),
            TypeCode.Int32 => ConstrainedCall<int>(value, destination, out bytesWritten),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value, destination, out bytesWritten),
            TypeCode.Int64 => ConstrainedCall<long>(value, destination, out bytesWritten),
            _ => EnumHelpers.Fail(out bytesWritten),
        };
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ConstrainedCall<TValue>(T value, Span<byte> destination, out int bytesWritten)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return TSerializer.TryWrite(Unsafe.BitCast<T, TValue>(value), destination, out bytesWritten);
        }
    }
    
    private int Write<TSerializer>(Span<byte> destination)
        where TSerializer : EnumHelpers.ISerializer, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value, destination),
            TypeCode.SByte => ConstrainedCall<sbyte>(value, destination),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value, destination),
            TypeCode.Int16 => ConstrainedCall<short>(value, destination),
            TypeCode.UInt32 => ConstrainedCall<uint>(value, destination),
            TypeCode.Int32 => ConstrainedCall<int>(value, destination),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value, destination),
            TypeCode.Int64 => ConstrainedCall<long>(value, destination),
            _ => 0,
        };
        
        static int ConstrainedCall<TValue>(T value, Span<byte> destination)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return TSerializer.Write(Unsafe.BitCast<T, TValue>(value), destination);
        }
    }

    /// <inheritdoc/>
    public int WriteLittleEndian(Span<byte> destination)
        => Write<LittleEndianness>(destination);

    /// <inheritdoc/>
    public int WriteBigEndian(Span<byte> destination)
        => Write<BigEndianness>(destination);
}

partial class EnumHelpers
{
    internal interface ISerializer
    {
        static abstract bool TryRead<TValue>(ReadOnlySpan<byte> source, bool isUnsigned, out TValue value)
            where TValue : unmanaged, IBinaryInteger<TValue>;

        static abstract TValue Read<TValue>(ReadOnlySpan<byte> source, bool isUnsigned)
            where TValue : unmanaged, IBinaryInteger<TValue>;

        static abstract bool TryWrite<TValue>(TValue value, Span<byte> destination, out int bytesWritten)
            where TValue : unmanaged, IBinaryInteger<TValue>;

        static abstract int Write<TValue>(TValue value, Span<byte> destination)
            where TValue : unmanaged, IBinaryInteger<TValue>;
    }
}

file readonly ref struct LittleEndianness : EnumHelpers.ISerializer
{
    static bool EnumHelpers.ISerializer.TryRead<TValue>(ReadOnlySpan<byte> source, bool isUnsigned, out TValue value)
        => TValue.TryReadLittleEndian(source, isUnsigned, out value);

    static TValue EnumHelpers.ISerializer.Read<TValue>(ReadOnlySpan<byte> source, bool isUnsigned)
        => TValue.ReadLittleEndian(source, isUnsigned);

    static bool EnumHelpers.ISerializer.TryWrite<TValue>(TValue value, Span<byte> destination, out int bytesWritten)
        => value.TryWriteLittleEndian(destination, out bytesWritten);

    static int EnumHelpers.ISerializer.Write<TValue>(TValue value, Span<byte> destination)
        => value.WriteLittleEndian(destination);
}

file readonly ref struct BigEndianness : EnumHelpers.ISerializer
{
    static bool EnumHelpers.ISerializer.TryRead<TValue>(ReadOnlySpan<byte> source, bool isUnsigned, out TValue value)
        => TValue.TryReadBigEndian(source, isUnsigned, out value);
    
    static TValue EnumHelpers.ISerializer.Read<TValue>(ReadOnlySpan<byte> source, bool isUnsigned)
        => TValue.ReadBigEndian(source, isUnsigned);

    static bool EnumHelpers.ISerializer.TryWrite<TValue>(TValue value, Span<byte> destination, out int bytesWritten)
        => value.TryWriteBigEndian(destination, out bytesWritten);
    
    static int EnumHelpers.ISerializer.Write<TValue>(TValue value, Span<byte> destination)
        => value.WriteBigEndian(destination);
}