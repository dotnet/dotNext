using System.Reflection;

namespace DotNext
{
    public sealed class ResourceStringTests : Test
    {
        [Fact]
        public static void CheckResourceStrings()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName.StartsWith("DotNext"))
                {
                    var exceptionMessagesType = asm.GetType("DotNext.ExceptionMessages");
                    if (exceptionMessagesType is null)
                        continue;

                    All(exceptionMessagesType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static), static property =>
                    {
                        IsType<string>(property.GetValue(null));
                    });
                }
            }
        }
    }
}