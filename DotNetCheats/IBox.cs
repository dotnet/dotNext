using System;

namespace Cheats
{
    /// <summary>
    /// Represents on-heap box of value type.
    /// </summary>
    /// <typeparam name="T">Boxed value type.</typeparam>
    public interface IBox<T>: ICloneable
        where T: struct
    {
        /// <summary>
        /// Unbox value type.
        /// </summary>
        /// <returns>Unboxed value type.</returns>
        T Unbox();
    }
}