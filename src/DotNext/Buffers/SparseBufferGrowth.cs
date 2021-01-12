namespace DotNext.Buffers
{
    /// <summary>
    /// Determines how the size of the subsequent memory chunk must be calculated.
    /// </summary>
    public enum SparseBufferGrowth
    {
        /// <summary>
        /// Each memory chunk has identical size.
        /// </summary>
        None = 0,

        /// <summary>
        /// The size of the new memory chunk is a multiple of the chunk index.
        /// </summary>
        Linear = 1,

        /// <summary>
        /// Each new memory chunk doubles in size.
        /// </summary>
        Exponential = 2,
    }
}