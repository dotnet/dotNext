using System;

namespace Cheats
{
    internal interface IBox<T>: ICloneable
        where T: struct
    {
         T Unbox();
    }
}