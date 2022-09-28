using System.Runtime.CompilerServices;

namespace DotNext.Workflow;

[AsyncMethodBuilder(typeof(ActivityBuilder))]
public struct ActivityResult
{
    public ActivityResultAwaiter GetAwaiter() => new();
}