using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNext.Reflection;

[TestClass]
public class TaskTypeTests
{
    [TestMethod]
    public void IsCompletedSuccessfullyPropertyGetter()
    {
        Assert.IsTrue(TaskType.IsCompletedSuccessfullyGetter(Task.CompletedTask));
    }

    [TestMethod]
    public void GetResultSynchronously()
    {
        Assert.AreEqual(42, TaskType.GetResultGetter<int>().Invoke(Task.FromResult(42)));
    }

    [TestMethod]
    public void IsCompletedPropertyGetter()
    {
        Assert.IsTrue(Task.CompletedTask.GetIsCompletedGetter().Invoke());
    }
}