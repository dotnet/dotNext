namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents memory access mode.
    /// </summary>
    public enum MemoryAccess: byte
    {
        /// <summary>
        /// Default memory access mode implemented by .NET Runtime.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Represents aligned memory access which is potentially optimized at harware level.
        /// </summary>
        Aligned = 1,

        /// <summary>
        /// Represents unaligned memory access.
        /// </summary>
        /// <remarks>
        /// Unaligned memory access may not be supported on all hardware platforms and this capability
        /// depends on CPU. 
        /// </remarks>
        /// <seealso href="https://www.kernel.org/doc/Documentation/unaligned-memory-access.txt">Unaligned memory access</seealso>
        Unaligned = 2
    }
}