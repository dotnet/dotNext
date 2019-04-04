using System;
using Xunit;

namespace DotNext
{
    public sealed class EnumTests: Assert
    {
        [Fact]
        public void ValuesTest()
        {
            True(Enum<EnvironmentVariableTarget>.IsDefined(nameof(EnvironmentVariableTarget.Machine)));
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.GetMember(nameof(EnvironmentVariableTarget.Process)));
            Equal(nameof(EnvironmentVariableTarget.User), Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.User).Name);
            Equal(nameof(EnvironmentVariableTarget.Process), default(Enum<EnvironmentVariableTarget>).Name);
        }

        [Fact]
        public void MinMaxTest()
        {
            Equal(EnvironmentVariableTarget.Machine, Enum<EnvironmentVariableTarget>.MaxValue);
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.MinValue);
        }
    }
}
