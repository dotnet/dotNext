namespace DotNext.Workflow;

/// <summary>
/// Identifies activity instance.
/// </summary>
/// <param name="InstanceName">The name of the instance.</param>
/// <param name="ActivityName">The activity name.</param>
public readonly record struct ActivityInstance(string InstanceName, string ActivityName);