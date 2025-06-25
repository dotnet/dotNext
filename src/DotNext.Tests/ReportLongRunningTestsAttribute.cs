using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Xunit.Sdk;

namespace DotNext;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ReportLongRunningTestsAttribute : BeforeAfterTestAttribute
{
    private readonly ConcurrentDictionary<MethodInfo, Timer> longRunningTests;
    private readonly TimeSpan timeout;
    
    public ReportLongRunningTestsAttribute(int timeoutMillis)
    {
        longRunningTests = new();
        timeout = TimeSpan.FromMilliseconds(timeoutMillis);
    }
    
    public override void Before(MethodInfo methodUnderTest)
    {
        var timer = new Timer(OnTimeout, methodUnderTest, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (longRunningTests.TryAdd(methodUnderTest, timer))
        {
            timer.Change(timeout, Timeout.InfiniteTimeSpan);
        }
        else
        {
            timer.Dispose();
        }

        static void OnTimeout(object state)
        {
            if (state is MethodInfo testMethod)
            {
                var output = $"Test method {testMethod.DeclaringType?.Name}:{testMethod.Name} is timed out";
                Debug.WriteLine(output);
                Console.WriteLine(output);
            }
        }
    }

    public override void After(MethodInfo methodUnderTest)
    {
        if (longRunningTests.TryRemove(methodUnderTest, out var timer))
        {
            timer.Dispose();
        }
    }
}