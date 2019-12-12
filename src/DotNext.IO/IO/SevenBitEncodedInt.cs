namespace DotNext.IO
{
    internal static class SevenBitEncodedInt
    {
        internal interface IWriter
        {
            void WriteByte(byte value);
        }

        internal static void Encode<TWriter>(ref TWriter writer, uint value)
            where TWriter : struct, IWriter
        {
            while (value >= 0x80)
            {
                writer.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            writer.WriteByte((byte)value);
        }
    }
}
