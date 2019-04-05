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
        public void Members()
        {
            Equal(3, Enum<EnvironmentVariableTarget>.Members.Count);
        }

        [Fact]
        public void ConversionToPrimitive()
        {
            var member = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.User);
            Equal(1L, member.Value.ToInt64());
            Equal(1, member.Value.ToInt32());
            Equal(1, member.Value.ToInt16());
            Equal(1, member.Value.ToByte());
        }

        [Fact]
        public void ConversionFromPrimitive()
        {
            Equal(EnvironmentVariableTarget.User, 1.ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.User, 1L.ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.Machine, ((byte)2).ToEnum<EnvironmentVariableTarget>());
        }

        [Fact]
        public void MinMaxTest()
        {
            Equal(EnvironmentVariableTarget.Machine, Enum<EnvironmentVariableTarget>.MaxValue);
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.MinValue);
        }
    }
}
