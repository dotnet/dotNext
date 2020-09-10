using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Resources;

namespace DotNext.Resources
{
    /// <summary>
    /// Provides access to resource strings and other
    /// resource objects via dynamic member access.
    /// </summary>
    public sealed class DynamicResourceManager : ResourceManager, IDynamicMetaObjectProvider
    {
        /// <summary>
        /// Initializes a new instance of dynamic resource manager that
        /// looks up resources in satellite assemblies based on information from the specified
        /// type object.
        /// </summary>
        /// <param name="resourceSource">
        /// A type from which the resource manager derives
        /// all information for finding .resources files.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="resourceSource"/> is <see langword="null"/>.</exception>
        public DynamicResourceManager(Type resourceSource)
            : base(resourceSource)
        {
        }

        /// <summary>
        /// Initializes a new instance of dynamic resource manager that
        /// looks up resources contained in files with the specified root name in the given
        /// assembly.
        /// </summary>
        /// <param name="baseName">
        /// The root name of the resource file without its extension but including any fully
        /// qualified namespace name.
        /// </param>
        /// <param name="assembly">The main assembly for the resources.</param>
        /// <exception cref="ArgumentNullException"><paramref name="baseName"/> or <paramref name="assembly"/> is <see langword="null"/>.</exception>
        public DynamicResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly)
        {
        }

        /// <inheritdoc />
        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
            => new ResourceManagerMetaObject(parameter, this);
    }
}