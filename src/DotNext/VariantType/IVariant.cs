using System.Dynamic;

namespace DotNext.VariantType
{
    /// <summary>
    /// A root interface for all variant data containers.
    /// </summary>
    public interface IVariant : IDynamicMetaObjectProvider
    {
        /// <summary>
        /// Gets value stored in the container.
        /// </summary>
        object Value { get; }

        /// <summary>
        /// Determines whether the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container.
        /// </summary>
        /// <typeparam name="V">The type of variant container.</typeparam>
        /// <param name="other">Other variant value to compare.</param>
        /// <returns>
        /// <see langword="true"/>, if the value stored in this variant
        /// container is equal to the value stored in the given variant
        /// container; otherwise, <see langword="false"/>.
        /// </returns>
        bool Equals<V>(V other)
            where V : IVariant;
    }
}