using System;
using Xunit;

namespace DotNext
{
    public sealed class EnumTests: Assert
    {
        [Fact]
        public void ValuesTest()
        {
            True(Enum<EnvironmentVariableTarget>.Names.ContainsKey(nameof(EnvironmentVariableTarget.Machine)));
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.Names[nameof(EnvironmentVariableTarget.Process)]);
            Equal(nameof(EnvironmentVariableTarget.User), Enum<EnvironmentVariableTarget>.Values[EnvironmentVariableTarget.User]);
        }

        [Fact]
        public void MinMaxTest()
        {
            Equal(EnvironmentVariableTarget.Machine, Enum<EnvironmentVariableTarget>.MaxValue);
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.MinValue);
        }
    }
}
