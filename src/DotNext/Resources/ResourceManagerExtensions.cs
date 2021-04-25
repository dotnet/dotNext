using System;
using System.Resources;
using System.Runtime.CompilerServices;

namespace DotNext.Resources
{
    /// <summary>
    /// Represents extension methods for <see cref="ResourceManager"/> class.
    /// </summary>
    public static class ResourceManagerExtensions
    {
        /// <summary>
        /// Gets typed resource using name of the caller member.
        /// </summary>
        /// <param name="manager">The resource manager.</param>
        /// <param name="name">The name that is filled by compiler automatically.</param>
        /// <returns>The resource entry.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="manager"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
        public static ResourceEntry Get(this ResourceManager manager, [CallerMemberName]string name = "")
            => new (manager, name);
    }
}