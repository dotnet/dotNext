using System;

namespace DotNext
{
    /// <summary>
    /// Indicates whether an endpoint of some range is contained in the range itself ("closed") or not ("open").
    /// </summary>
    /// <remarks>
    /// If a range is unbounded on a side, it is neither open nor closed on that side; the bound simply does not exist.
    /// </remarks>
    [Flags]
    public enum BoundType : byte
    {
        /// <summary>
        /// Both endpoints are not considered part of the set: (X, Y)
        /// </summary>
        Open = 0,

        /// <summary>
        /// The left endpoint value is considered part of the set: [X, Y)
        /// </summary>
        LeftClosed = 0x01,

        /// <summary>
        /// The right endpoint value is considered part of the set: (X, Y]
        /// </summary>
        RightClosed = 0x02,

        /// <summary>
        /// Both endpoints are considered part of the set.
        /// </summary>
        Closed = LeftClosed | RightClosed,
    }
}