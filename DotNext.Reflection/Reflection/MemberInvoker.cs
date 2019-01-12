namespace DotNext.Reflection
{
    /// <summary>
    /// Represents generic method invoker.
    /// </summary>
    /// <param name="args">Invocation arguments.</param>
    /// <typeparam name="S">A structure describing arguments including hidden <see langword="this"/> parameter and return type.</typeparam>
    public delegate void MemberInvoker<S>(in S args)
        where S: struct;
}
