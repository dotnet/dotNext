using System;

namespace Cheats
{
    public interface IBox<T>: ICloneable
        where T: struct
    {
         T Unbox();
    }
}