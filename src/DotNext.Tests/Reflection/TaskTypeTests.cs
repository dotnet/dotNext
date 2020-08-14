using System.Threading.Tasks;
using Xunit;

namespace DotNext.Reflection
{
    public sealed class TaskTypeTests : Test
    {
        [Fact]
        public static void ReflectTaskType()
        {
            var task = typeof(Task);
            Equal(typeof(void), task.GetTaskType());
            Equal(typeof(Task), typeof(void).MakeTaskType());
            task = typeof(Task<int>);
            Equal(typeof(int), task.GetTaskType());
            Equal(typeof(Task<int>), typeof(int).MakeTaskType());
        }
    }
}
