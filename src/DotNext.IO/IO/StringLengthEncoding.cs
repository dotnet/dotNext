namespace DotNext.IO
{
    /// <summary>
    /// Describes how string length should be encoded in binary form.
    /// </summary>
    public enum StringLengthEncoding : byte
    {
        /// <summary>
        /// Do not read or write string length.
        /// </summary>
        None = 0,

        /// <summary>
        /// Use 4-byte to represent string length.
        /// </summary>
        Plain,

        /// <summary>
        /// Use 7-bit encoding compressed format.
        /// </summary>
        SevenBitEncoded
    }
}
