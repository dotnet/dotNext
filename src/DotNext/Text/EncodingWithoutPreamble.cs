using System;
using System.Text;

namespace DotNext.Text
{
    //TODO: Should have additional overrides for .NET Standard 2.1
    internal sealed class EncodingWithoutPreamble : Encoding
    {
        private readonly Encoding encoding;

        private EncodingWithoutPreamble(Encoding enc) : base(enc.WindowsCodePage, enc.EncoderFallback, enc.DecoderFallback) => encoding = enc;

        internal static Encoding Create(Encoding enc) => new EncodingWithoutPreamble(enc);

        public override byte[] GetPreamble() => Array.Empty<byte>();

        public override int GetByteCount(char[] chars, int index, int count)
            => encoding.GetByteCount(chars, index, count);

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
            => encoding.GetBytes(chars, charIndex, charCount, bytes, byteIndex);

        public override int GetCharCount(byte[] bytes, int index, int count)
            => encoding.GetCharCount(bytes, index, count);

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            => encoding.GetChars(bytes, byteIndex, byteCount, chars, charIndex);

        public override int GetMaxByteCount(int charCount)
            => encoding.GetMaxByteCount(charCount);

        public override int GetMaxCharCount(int byteCount)
            => encoding.GetMaxCharCount(byteCount);

        public override string BodyName => encoding.BodyName;

        public override int CodePage => encoding.CodePage;

        public override string EncodingName => encoding.EncodingName;

        public override unsafe int GetByteCount(char* chars, int count)
            => encoding.GetByteCount(chars, count);

        public override int GetByteCount(char[] chars)
            => encoding.GetByteCount(chars);

        public override int GetByteCount(string s)
            => encoding.GetByteCount(s);

        public override byte[] GetBytes(char[] chars)
            => encoding.GetBytes(chars);

        public override string HeaderName => encoding.HeaderName;

        public override object Clone() => new EncodingWithoutPreamble(encoding);

        public override int WindowsCodePage => encoding.WindowsCodePage;

        public override string WebName => encoding.WebName;

        public override bool IsSingleByte => encoding.IsSingleByte;

        public override bool IsBrowserDisplay => encoding.IsBrowserDisplay;

        public override bool IsBrowserSave => encoding.IsBrowserSave;

        public override bool IsMailNewsDisplay => encoding.IsMailNewsDisplay;

        public override bool IsMailNewsSave => encoding.IsMailNewsSave;

        public override unsafe int GetBytes(char* chars, int charCount, byte* bytes, int byteCount)
            => encoding.GetBytes(chars, charCount, bytes, byteCount);

        public override byte[] GetBytes(char[] chars, int index, int count)
            => encoding.GetBytes(chars, index, count);

        public override byte[] GetBytes(string s)
            => encoding.GetBytes(s);

        public override int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex)
            => encoding.GetBytes(s, charIndex, charCount, bytes, byteIndex);

        public override unsafe int GetCharCount(byte* bytes, int count)
            => encoding.GetCharCount(bytes, count);

        public override int GetCharCount(byte[] bytes)
            => encoding.GetCharCount(bytes);

        public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount)
            => encoding.GetChars(bytes, byteCount, chars, charCount);

        public override char[] GetChars(byte[] bytes)
            => encoding.GetChars(bytes);

        public override bool IsAlwaysNormalized(NormalizationForm form)
            => encoding.IsAlwaysNormalized(form);

        public override char[] GetChars(byte[] bytes, int index, int count)
            => encoding.GetChars(bytes, index, count);

        public override Decoder GetDecoder() => encoding.GetDecoder();

        public override Encoder GetEncoder() => encoding.GetEncoder();

        public override string GetString(byte[] bytes) => encoding.GetString(bytes);

        public override string GetString(byte[] bytes, int index, int count)
            => encoding.GetString(bytes, index, count);

        public override string ToString() => encoding.ToString();

        public override bool Equals(object other)
            => other is EncodingWithoutPreamble wrapper ? encoding.Equals(wrapper.encoding) : encoding.Equals(other);

        public override int GetHashCode() => encoding.GetHashCode();
    }
}
