namespace DotNext.Reflection
{
    /// <summary>
    /// Represents invoker of a member.
    /// </summary>
    /// <remarks>
    /// Arguments dependending on the member:
    /// <list type="bullet">
    /// <listheader>
    /// <term>Field</term>
    /// <description>Read operation doesn't require arguments; Write operation requires single argument with field value.</description>
    /// </listheader>
    /// <listheader>
    /// <term>Method</term>
    /// <description>Arguments will be passed to the method as-is.</description>
    /// </listheader>
    /// </list>
    /// </remarks>
    /// <param name="target">Target object; for static members should be <see langword="null"/>.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The result of member invocation.</returns>
    public delegate object? DynamicInvoker(object? target, params object?[] args);  // TODO: args must be changed to ReadOnlySpan<object>
}
