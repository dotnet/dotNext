using System;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents method declaration type.
    /// </summary>
    [Serializable]
    public enum MethodLookup : byte
    {
        /// <summary>
        /// Represents static method.
        /// </summary>
        Static = 0,

        /// <summary>
        /// Represents instance method.
        /// </summary>
        Instance = 1,
    }
}