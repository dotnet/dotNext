namespace DotNext.Workflow;

public enum ActivityStatus
{
    Started = 0,

    Running,

    TimingOut,

    TimedOut,

    Canceling,

    Canceled,

    Failed,

    Completed,
}