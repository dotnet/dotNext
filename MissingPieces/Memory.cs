using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace MissingPieces
{
    /// <summary>
    /// Low-level operations with memory and spans.
    /// </summary>
    public static class Memory
    {
        /// <summary>
        /// Represents sequential memory reader.
        /// </summary>
        public static class Reader
        {

            /// <summary>
            /// Drops a number of bytes.
            /// </summary>
            /// <param name="span">Memory span to modify.</param>
            /// <param name="bytes">Number of bytes to drop.</param>
            public static void Drop(ref ReadOnlySpan<byte> span, int bytes)
                => span = span.Length > bytes ? span.Slice(bytes) : ReadOnlySpan<byte>.Empty;

            /// <summary>
            /// Reads single byte of memory.
            /// </summary>
            /// <param name="span">Memory span to modify.</param>
            /// <returns>Byte value.</returns>
            public static byte ReadByte(ref ReadOnlySpan<byte> span)
            {
                var result = span[0];
                Drop(ref span, 1);
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static byte ReadByte(ref IntPtr pointer)
            {
                var result = DereferenceByte(pointer);
                pointer += sizeof(byte);
                return result;
            }
            
            [CLSCompliant(false)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong ReadUInt64(ref IntPtr pointer)
            {
                var result = DereferenceUInt64(pointer);
                pointer += sizeof(ulong);
                return result;
            }

            [CLSCompliant(false)]
            public static ulong ReadUInt64(ref ReadOnlySpan<byte> span) 
                => BitConverter.IsLittleEndian ? ReadUInt64LittleEndian(ref span) : ReadUInt64BigEndian(ref span);

            [CLSCompliant(false)]
            public static ulong ReadUInt64BigEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadUInt64BigEndian(span);
                Drop(ref span, sizeof(ulong));
                return result;
            }

            [CLSCompliant(false)]
            public static ulong ReadUInt64LittleEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadUInt64LittleEndian(span);
                Drop(ref span, sizeof(ulong));
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static long ReadInt64(ref IntPtr pointer)
            {
                var result = DereferenceInt64(pointer);
                pointer += sizeof(long);
                return result;
            }

            public static long ReadInt64(ref ReadOnlySpan<byte> span) 
                => BitConverter.IsLittleEndian ? ReadInt16LittleEndian(ref span) : ReadInt16BigEndian(ref span);

            public static long ReadInt64BigEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadInt64BigEndian(span);
                Drop(ref span, sizeof(long));
                return result;
            }

            public static long ReadInt64LittleEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadInt64LittleEndian(span);
                Drop(ref span, sizeof(long));
                return result;
            }

            [CLSCompliant(false)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static uint ReadUInt32(ref IntPtr pointer)
            {
                var result = DereferenceUInt32(pointer);
                pointer += sizeof(uint);
                return result;
            }

            [CLSCompliant(false)]
            public static uint ReadUInt32(ref ReadOnlySpan<byte> span) 
                => BitConverter.IsLittleEndian ? ReadUInt32LittleEndian(ref span) : ReadUInt32BigEndian(ref span);

            [CLSCompliant(false)]
            public static uint ReadUInt32BigEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadUInt32BigEndian(span);
                Drop(ref span, sizeof(uint));
                return result;
            }

            [CLSCompliant(false)]
            public static uint ReadUInt32LittleEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadUInt32LittleEndian(span);
                Drop(ref span, sizeof(uint));
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int ReadInt32(ref IntPtr pointer)
            {
                var result = DereferenceInt32(pointer);
                pointer += sizeof(int);
                return result;
            }

            public static int ReadInt32(ref ReadOnlySpan<byte> span) 
                => BitConverter.IsLittleEndian ? ReadInt32LittleEndian(ref span) : ReadInt32BigEndian(ref span);

            public static int ReadInt32BigEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadInt32BigEndian(span);
                Drop(ref span, sizeof(int));
                return result;
            }

            public static int ReadInt32LittleEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadInt32LittleEndian(span);
                Drop(ref span, sizeof(int));
                return result;
            }

            [CLSCompliant(false)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ushort ReadUInt16(ref IntPtr pointer)
            {
                var result = DereferenceUInt16(pointer);
                pointer += sizeof(ushort);
                return result;
            }

            [CLSCompliant(false)]
            public static ushort ReadUInt16(ref ReadOnlySpan<byte> span) 
                => BitConverter.IsLittleEndian ? ReadUInt16LittleEndian(ref span) : ReadUInt16BigEndian(ref span);

            [CLSCompliant(false)]
            public static ushort ReadUInt16BigEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadUInt16BigEndian(span);
                Drop(ref span, sizeof(ushort));
                return result;
            }

            [CLSCompliant(false)]
            public static ushort ReadUInt16LittleEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadUInt16LittleEndian(span);
                Drop(ref span, sizeof(ushort));
                return result;
            }

            [CLSCompliant(false)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static short ReadInt16(ref IntPtr pointer)
            {
                var result = DereferenceInt16(pointer);
                pointer += sizeof(short);
                return result;
            }

            public static short ReadInt16(ref ReadOnlySpan<byte> span) 
                => BitConverter.IsLittleEndian ? ReadInt16LittleEndian(ref span) : ReadInt16BigEndian(ref span);

            public static short ReadInt16BigEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadInt16BigEndian(span);
                Drop(ref span, sizeof(short));
                return result;
            }

            public static short ReadInt16LittleEndian(ref ReadOnlySpan<byte> span)
            {
                var result = BinaryPrimitives.ReadInt16LittleEndian(span);
                Drop(ref span, sizeof(short));
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float ReadSingle(ref IntPtr pointer)
            {
                var result = DereferenceSingle(pointer);
                pointer += sizeof(float);
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double ReadDouble(ref IntPtr pointer)
            {
                var result = DereferenceDouble(pointer);
                pointer += sizeof(double);
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static decimal ReadDecimal(ref IntPtr pointer)
            {
                var result = DereferenceDecimal(pointer);
                pointer += sizeof(decimal);
                return result;
            }
        }

        private static class Ref<T>
        {
            internal delegate ref T Converter(in T value);

            internal static readonly Converter ToRegularRef;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ref T Identity(ref T value) => ref value;

            static Ref()
            {
                var parameter = Parameter(typeof(T).MakeByRefType());
                var identity = typeof(Ref<T>).GetMethod(nameof(Identity), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                ToRegularRef = Lambda<Converter>(Call(null, identity, parameter), parameter).Compile();
            }
        }

        /// <summary>
        /// Converts IN parameter into regular reference.
        /// </summary>
        /// <param name="value">A reference to convert.</param>
        /// <typeparam name="T">Type of reference.</typeparam>
        /// <returns>Converted reference.</returns>
        public static ref T AsRef<T>(in T value) => ref Ref<T>.ToRegularRef(in value);

        public static bool ContentAreEqual(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        {
            if(first.Length != second.Length)
                return false;
            tail_call: switch(first.Length)
            {
                case 0:
                    return true;
                case sizeof(byte): 
                    return Reader.ReadByte(ref first) == Reader.ReadByte(ref second);
                case sizeof(ushort): 
                    return Reader.ReadUInt16(ref first) == Reader.ReadUInt16(ref second);
                case sizeof(ushort) + sizeof(byte):
                    return Reader.ReadUInt16(ref first) == Reader.ReadUInt16(ref second) && Reader.ReadByte(ref first) == Reader.ReadByte(ref second);
                case sizeof(uint):
                    return Reader.ReadUInt32(ref first) == Reader.ReadUInt32(ref second);
                case sizeof(uint) + sizeof(byte):
                    return Reader.ReadUInt32(ref first) == Reader.ReadUInt32(ref second) && Reader.ReadByte(ref first) == Reader.ReadByte(ref second);
                case sizeof(uint) + sizeof(ushort):
                    return Reader.ReadUInt32(ref first) == Reader.ReadUInt32(ref second) && Reader.ReadUInt16(ref first) == Reader.ReadUInt16(ref second);
                case sizeof(uint) + sizeof(ushort) + sizeof(byte):
                    return Reader.ReadUInt32(ref first) == Reader.ReadUInt32(ref second) && 
                        Reader.ReadUInt16(ref first) == Reader.ReadUInt16(ref second) && 
                        Reader.ReadByte(ref first) == Reader.ReadByte(ref second);
                case sizeof(ulong):
                    return Reader.ReadUInt64(ref first) == Reader.ReadUInt64(ref second);
                default:
                    if(Reader.ReadUInt64(ref first) == Reader.ReadUInt64(ref second))
                        goto tail_call;
                    else
                        return false;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte DereferenceByte(this IntPtr pointer) => *(byte*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe short DereferenceInt16(this IntPtr pointer) => *(short*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe ushort DereferenceUInt16(this IntPtr pointer) => *(ushort*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int DereferenceInt32(this IntPtr pointer) => *(int*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe uint DereferenceUInt32(this IntPtr pointer) => *(uint*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe long DereferenceInt64(this IntPtr pointer) => *(long*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe ulong DereferenceUInt64(this IntPtr pointer) => *(ulong*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float DereferenceSingle(this IntPtr pointer) => *(float*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double DereferenceDouble(this IntPtr pointer) => *(double*)pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe decimal DereferenceDecimal(this IntPtr pointer) => *(decimal*)pointer;
    }
}