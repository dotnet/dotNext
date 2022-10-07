namespace DotNext.Workflow;

public sealed class InstancePersistenceException : IOException
{
    internal InstancePersistenceException(Exception e)
        : base(null, e)
    {
    }
}