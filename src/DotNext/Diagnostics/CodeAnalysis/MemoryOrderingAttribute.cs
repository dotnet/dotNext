namespace DotNext.Diagnostics.CodeAnalysis;

/// <summary>
/// Indicates the memory ordering guarantees that the marked method provides to the caller.
/// </summary>
/// <remarks>
/// The attribute is informational only and doesn't affect the code generation.
/// It specifies the weakest ordering the method guarantees on every platform. The actual ordering can be stronger
/// on some platforms or for some code paths, but the caller must not rely on that.
/// The absence of the attribute means that the method provides no documented guarantees.
/// To describe a property, apply the attribute to its accessors, since the getter and the setter
/// usually provide different guarantees.
/// </remarks>
/// <param name="ordering">The memory ordering guarantees provided by the marked method.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class MemoryOrderingAttribute(MemoryOrdering ordering) : Attribute
{
    /// <summary>
    /// Gets the memory ordering guarantees provided by the marked method.
    /// </summary>
    public MemoryOrdering Ordering => ordering;
}