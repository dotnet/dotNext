using System;

namespace MissingPieces
{
    [Flags]
    public enum BoundType: byte
    {
        Open = 0,

        LeftClosed = 0x01,

        RightClosed = 0x02,
        Closed = LeftClosed | RightClosed
    }
}