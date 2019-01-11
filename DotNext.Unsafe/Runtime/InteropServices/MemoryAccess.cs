namespace Cheats.Runtime.InteropServices
{
    /// <summary>
    /// Represents memory access mode.
    /// </summary>
    public enum MemoryAccess: byte
    {
        Default = 0,

        Aligned = 1,

        Unaligned = 2
    }
}